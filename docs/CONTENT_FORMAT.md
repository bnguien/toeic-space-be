# 🧾 Định dạng nội dung đề thi

Dữ liệu Assessment được nhập từ Studychill (hai nguồn gốc: **dautoeic** và **toeicmentors**). Các cột văn bản không phải text thuần; bảng dưới mô tả những gì có trong dữ liệu và cách API trả về.

## 1. Dữ liệu gốc

| Cột | Part | Định dạng |
|---|---|---|
| `ToeicPassages.Content` | 3, 4 | Script hội thoại: text thuần (`M-Au: …` mỗi dòng) hoặc HTML `<p><strong>M:</strong> …</p>` |
| | 6 | HTML. Chỗ trống: `____135____`, `______[135]` (đề thi, bộ Part 6 theo level) hoặc `<span class="tp-p6-blank" data-qnum="13">[13]</span>` (ngân hàng, số thứ tự nội bộ) |
| | 7 | HTML (`div.info/email/article…`, bảng, danh sách, style inline, link) |
| | tất cả | Có thể kèm `<translation_split>` + **bản dịch tiếng Việt** (text thuần hoặc HTML) |
| `ToeicQuestions.Transcript` | 1–4 | HTML hoặc text thuần; Part 3/4 có thể kèm `<hr/>` + bản dịch |
| `ToeicQuestions.Explanation` | tất cả | HTML của Studychill (`tp-exp-*`: thẻ đáp án đúng, dòng đúng/sai, ghi chú, dịch nghĩa, từ vựng — đôi khi là mảng JSON), có markdown `*…*`, `**…**` |
| | 5, 6, 7 | Đề Crack TOEIC / Pass TOEIC không có lời giải trong database Studychill; pipeline dựng lại từ bộ dữ liệu thô dautoeic (`explanation_vi`, `dich_nghia`, `tu_vung`, `dich_nghia_dap_an`) theo đúng cấu trúc `tp-exp-*`, và từ `data/manual/explanations.json` cho vài câu không nguồn nào có. Câu thuộc đoạn văn (Part 3/4/6/7) **không** kèm thẻ "Dịch nghĩa" vì `dich_nghia` chính là bản dịch của đoạn văn, đã hiển thị ở phần đoạn văn. Lời giải gốc luôn được ưu tiên khi gộp câu trùng |
| `QuestionText`, `OptionA–D` | tất cả | Text thuần, có entity (`&eacute;`), `*tên sách*`; Part 5 dùng `-------` làm chỗ trống; câu đầu Part 6 đôi khi chứa tiêu đề "Questions 131-134 refer to…"; bộ Part 6 theo level dùng `[Chỗ trống N]` |
| `ImageUrl` (passage, question) | 7 | Một URL, hoặc nhiều trang nối bằng `<image_split>` (`MediaUrls`). `AudioUrl` luôn là một URL |

## 2. Hợp đồng API

`TranslatedText` (Application/Common/Content) tách bản dịch khi đọc dữ liệu:

| DTO | Trường gốc | Trường bản dịch |
|---|---|---|
| `PassageDto`, `PassageDetailDto`, `ContentPassageDto` | `content` | `contentTranslation` |
| `QuestionDetailDto`, `ContentQuestionDto` | `transcript` | `transcriptTranslation` |

- Học viên làm bài (`/tests/{id}/full` không có đáp án): **không** nhận bản dịch, lời giải, transcript. Bản dịch Part 6 chứa luôn câu cần điền nên cũng bị ẩn.
- Content manager và phần luyện tập có đáp án: nhận đủ hai trường.
- Cache key có tiền tố `assessment:content:v2`; khi đổi hình dạng payload thì tăng version.

## 3. Ghi dữ liệu

`RichTextRules.MustBeSafeRichText()` từ chối thẻ `script/style/iframe/object/embed/form/svg…`, thuộc tính `on*=` và URL `javascript:/vbscript:/data:` trong mọi trường nội dung. HTML đơn giản (đoạn văn, bảng, danh sách, class `tp-*`) vẫn hợp lệ.

URL media phải là http(s) tuyệt đối, không chứa `<`, `>`, `"` hay ký tự điều khiển (khoảng trắng được phép vì tên file nguồn có dấu cách). `ImageUrl` dùng `MustBeOptionalHttpUrls()`: kiểm tra từng URL sau khi tách `<image_split>`, không cho phần rỗng.

## 4. Hiển thị (frontend)

Dùng `RichText` (`toeic-space-fe/src/shared/components/RichText`), **không** dùng `dangerouslySetInnerHTML`:

- HTML được phân tích trong tài liệu trơ (`DOMParser`) rồi dựng lại bằng React theo allow-list thẻ và class; style inline, link, media, sự kiện bị loại.
- Text thuần giữ xuống dòng, giải mã entity, in đậm nhãn người nói.
- Chỗ trống hiển thị thành chip số câu; ở ngân hàng, số nội bộ được đổi sang câu tương ứng của đoạn.
- Ảnh: `mediaUrls()` tách `<image_split>` và chỉ giữ URL http(s). Câu hỏi thường lưu lại đúng media của đoạn văn (1.837 ảnh, 5.355 audio), nên phần xem trước bỏ qua media đã hiện ở đoạn văn.
- `toPlainText()` dùng cho danh sách và tiêu đề một dòng.
