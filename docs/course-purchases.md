# Course purchases (PBL-57)

Payment HTTP endpoints dispatch MediatR commands and queries in
`Payment.Application/Features/{Coupons,Orders,SePay,Reconciliation}`. Contracts and DTOs live in
Application, domain entities and enums in Domain, while MySQL mappings, migrations, repositories
and external clients remain in `Payment.Infrastructure/Persistence` and `Payment.Infrastructure/Services`.

If a bank transfer arrives but `PaymentTransactions` has no row, check SePay webhook delivery
history first. The receiver requires `POST https://<public-host>/payment/api/v1/webhooks/sepay`
with `Authorization: Apikey <configured-key>`; localhost and Docker service names are not
reachable by SePay. A missing/wrong key yields 401, malformed JSON yields 400, and a successful
delivery returns `200 {"success":true}`. A validly authenticated payload is stored even when its
account, amount, reference, or order state is invalid (`Invalid`, `Unmatched`, `Late`, etc.).
After deploying the updated Payment API, application logs record only webhook HTTP status and
transaction classification (not headers, credentials, bank account, or transfer memo). Compare
SePay's delivery timestamp/status to these logs, and verify you are inspecting the Payment DB
used by the running container. Do not re-send a webhook by hand unless it comes from the
verified SePay delivery history: bank receipt alone must never mark an order paid.

Public course summaries/details include `remainingSeats` (null for unlimited capacity); this is
an informational snapshot. Checkout/free enrollment still enforces capacity under a database lock.
`GET /api/v1/enrollments/me` includes a five-minute signed `thumbnailUrl` for each course with a
thumbnail. `GET /api/v1/admin/coupons?search=&isActive=&page=1&pageSize=20` returns
`{page,pageSize,totalCount,items}` (page size 1–100). Coupon items and the coupon detail endpoint
include `usedCount` (redeemed), `reservedCount` (held), and nullable `remainingUses` (limit minus
redeemed and held); released reservations do not consume the limit. Existing coupon fields remain
flat in each item. The coupon list response changed from an array to a paged object; clients must
update accordingly.

Course Promotions are owned by Content (`/api/v1/admin/course-promotions`), with half-open UTC
windows `[startAt, expiresAt)`. Active schedules for the same explicitly selected course cannot
overlap. Public catalog/detail include `promotion` with original price, discount, selling price,
and expiry; these are previews, not commitments. Payment compares the eligible Promotion with an
optional eligible Coupon, selects the larger discount (Promotion wins ties), and snapshots the
original amount, discount amount/source, promotion ID/name or Coupon code, and final payable
amount in the Order. Neither discounts nor base prices are recomputed for an existing Pending
Order during its payment window; cancel/expire it before creating a fresh Order at current prices.
Payment holds only the chosen discount's usage. Content reserves Promotion usage by checkout
reference with a row lock; Payment and Content retry release/confirmation independently after
cancel/expiry/payment. Content reconciles orphan reservations with Payment after their deadline.
For a full discount, Payment creates a Paid order with no bank transaction, then confirms
Promotion usage before enrollment fulfillment. Migrations are applied only to disposable test
schemas until explicitly deployed; deploy Content migrations and API before Payment migrations
and API to avoid missing internal Promotion routes.

## Deployment

Configure `PAYMENT_DB_*`, `PAYMENT_API_PORT`, `INTERNAL_API_KEY` (same random 32+ character value in
Content and Payment), `SEPAY_BANK`, `SEPAY_ACCOUNT_NUMBER`, and `SEPAY_WEBHOOK_API_KEY` in the deployment
environment. `CHECKOUT_RESERVATION_MINUTES` defaults to 15 and must agree between services.
Payment and Content use separate MySQL databases. Do not expose either service's `/api/v1/internal/*`
endpoints on a public ingress; the YARP gateway rejects them. Production ingress must provide HTTPS.

Before starting the updated APIs, apply EF migrations independently:

```bash
ConnectionStrings__ContentDatabase='Server=...;Database=toeic_space_content;...' \
  dotnet ef database update --project src/Services/Content/ToeicSpace.Content.Infrastructure \
  --startup-project src/Services/Content/ToeicSpace.Content.Infrastructure
ConnectionStrings__PaymentDatabase='Server=...;Database=toeic_space_payment;...' \
  dotnet ef database update --project src/Services/Payment/ToeicSpace.Payment.Infrastructure \
  --startup-project src/Services/Payment/ToeicSpace.Payment.Infrastructure
```

Configure a SePay webhook for incoming transfers on the configured receiving account:
`https://<public-host>/payment/api/v1/webhooks/sepay`, JSON body, **API Key** authentication,
with the same value as `SEPAY_WEBHOOK_API_KEY`. SePay sends `Authorization: Apikey <key>`.
Enable retries for non-2xx responses. The API acknowledges persisted notifications with
`200 {"success":true}`. The SePay transaction ID is unique in Payment DB.

QR image URLs use the SePay-documented `https://vietqr.app/img` link format with bank, account,
immutable amount and unique payment reference. Do not use a different QR generation service.
Official references: https://docs.sepay.vn/tich-hop-webhooks.html and
https://docs.sepay.vn/tao-qr-code-vietqr-dong.html.

## Routes (all paths below are relative to each service; prepend `/content` or `/payment` at YARP)

Content:

- `PUT /api/v1/courses/{id}/pricing` (Admin): `{ "price": 100000, "capacity": 20 }`; null capacity is unlimited.
- `POST /api/v1/enrollments/{courseId}` (User): only for published free courses.
- `GET /api/v1/enrollments/{courseId}` and `GET /api/v1/enrollments/me?page=1&pageSize=20&search=...` (User).
- `GET /api/v1/enrollments/{courseId}/lessons/{lessonId}` (User): published course and lesson,
  active enrollment; private video/attachment URLs are signed for five minutes.
- Existing public catalog, detail and free preview routes remain anonymous.

Payment:

- `POST/GET /api/v1/admin/coupons`, `GET/PUT /api/v1/admin/coupons/{id}`,
  `POST /api/v1/admin/coupons/{id}/deactivate` (Admin).
- `GET /api/v1/orders/preview?courseId=...&couponCode=...` (User): authoritative Content price.
- `POST /api/v1/orders` (User): `{ "courseId": "...", "checkoutReference": "<client-generated GUID>",
  "couponCode": null }`. Retry with the same reference; one pending order per user/course.
- `GET /api/v1/orders/{id}` and `POST /api/v1/orders/{id}/cancel` (owner User).
- `POST /api/v1/webhooks/sepay` (SePay API Key; no User JWT).
- `GET /api/v1/admin/payment-reconciliation/transactions` and `/orders` (Admin): review exceptions.

The order's price/coupon snapshot does not change after creation. Full-discount coupons
complete the order without SePay transactions. Valid paid orders persist `Fulfillment=Pending`
and the Payment worker retries Content enrollment after temporary failures. Content holds reserved
capacity until Payment confirms or releases it; abandoned reservations are reconciled after expiry.
Late, invalid, underpaid, overpaid, ambiguous and unmatched bank transfers remain in `PaymentTransactions`
for manual reconciliation. `Fulfillment=NeedsReview` flags paid orders that could not claim a slot.
No automatic refund is performed.

## Tests

Set `PBL57_CONTENT_TEST_DB` and `PBL57_PAYMENT_TEST_DB` to **dedicated, migrated MySQL test schemas**
to run the MySQL concurrency, transaction, expiry and webhook tests. Without those variables,
only pure/unit tests run. Never point these variables at a populated service database.

No live SePay payment or webhook delivery is covered by automated tests. Verify the HTTPS ingress,
SePay dashboard account/filter/auth configuration and a real small transfer before production use.
