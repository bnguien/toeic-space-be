# 📘 TOEIC Space Backend - Coding Conventions

Tài liệu này quy định các convention chính khi viết C# cho TOEIC Space Backend.

---

## 1. Naming Conventions

| Thành phần | Quy tắc | Ví dụ |
|---|---|---|
| Namespace | `PascalCase`, bám theo folder | `ToeicSpace.Identity.Application.Users` |
| Class / Struct | `PascalCase`, danh từ số ít | `User`, `Course` |
| Interface | `I` + `PascalCase` | `ITokenService` |
| Method | `PascalCase`, động từ; async có hậu tố `Async` | `GetByIdAsync()` |
| Property | `PascalCase` | `CreatedAt` |
| Biến local | `camelCase` | `userId` |
| Parameter | `camelCase` | `cancellationToken` |
| Enum | `PascalCase`, danh từ số ít | `UserStatus` |
| Constant | `PascalCase` | `DefaultPageSize` |

---

## 2. Tổ chức code theo độ phức tạp của service

### Service đơn giản

Có thể chỉ dùng một API project và tổ chức theo feature:

```text
<Service>.API/
├── Data/
├── Models/
├── Exceptions/
├── <Feature>/
├── Program.cs
└── DependencyInjection.cs
```

### Service phức tạp

Tách thành 4 project:

```text
<Service>.API
<Service>.Application
<Service>.Domain
<Service>.Infrastructure
```

Trong `Application`, ưu tiên tổ chức theo feature/use case:

```text
Users/
├── Commands/
├── Queries/
└── EventHandlers/
```

---

## 3. CQRS Conventions

- **Command**: dùng cho thao tác thay đổi dữ liệu.
- **Query**: dùng cho thao tác đọc dữ liệu.
- **Handler**: xử lý một use case cụ thể.
- **Validator**: dùng FluentValidation và đặt gần Command/Query tương ứng.

Ví dụ:

```text
Users/
└── Commands/
    └── CreateUser/
        ├── CreateUserCommand.cs
        ├── CreateUserHandler.cs
        └── CreateUserValidator.cs
```

---

## 4. DTO & Response Naming

- Request thay đổi dữ liệu: `CreateUserCommand`, `UpdateCourseCommand`
- Request đọc dữ liệu: `GetUserByIdQuery`
- DTO: hậu tố `Dto`
- Response/Result: hậu tố `Response` hoặc `Result`

Ví dụ:

```text
UserDto
AuthResponse
CreateCourseResult
```

---

## 5. Dependency Injection

Đăng ký dependency trong `DependencyInjection.cs` của từng project/service.

Ưu tiên lifetime:

```text
Transient  → helper nhẹ, stateless
Scoped     → DbContext, repository, application service
Singleton  → service thực sự thread-safe và dùng toàn app
```

Không đăng ký dependency rải rác trong nhiều file nếu có thể gom về DI extension.

---

## 6. Async & CancellationToken

Method bất đồng bộ phải:

- trả về `Task` / `Task<T>`
- có hậu tố `Async`
- truyền `CancellationToken` xuống database/external call

```csharp
public async Task<User?> GetByIdAsync(
    Guid id,
    CancellationToken cancellationToken)
{
    return await dbContext.Users
        .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
}
```

---

## 7. Logging

Dùng structured logging:

```csharp
logger.LogInformation(
    "User {UserId} logged in successfully",
    userId);
```

Không dùng string interpolation cho log:

```csharp
logger.LogInformation($"User {userId} logged in"); // Không nên
```

---

## 8. Exception Handling

Dùng custom exception phù hợp:

```text
BadRequestException
NotFoundException
ConflictException
ForbiddenException
```

Không `try/catch` ở mọi handler nếu exception đã được xử lý tập trung bởi global exception handler/middleware.

---

## 9. EF Core & Query

Query chỉ đọc dữ liệu nên dùng:

```csharp
.AsNoTracking()
```

Luôn truyền:

```csharp
cancellationToken
```

Tránh load dữ liệu không cần thiết. Chỉ `Include(...)` relation thực sự cần dùng.

---

## 10. Nullable Reference Types

Project bật Nullable Reference Types.

Biến có thể null phải khai báo rõ:

```csharp
string? avatarUrl;
Guid? roleId;
```

Dữ liệu bắt buộc nên dùng `required` hoặc cấu hình validation phù hợp.

---

## 11. Mapping

Mapping giữa Entity và DTO nên tách riêng, không viết mapping phức tạp lặp lại trong handler.

Có thể dùng mapper hoặc extension method tùy service.

Ví dụ:

```csharp
public static UserDto ToDto(this User user)
{
    return new UserDto(
        user.Id,
        user.Email);
}
```

---

## 12. Quy tắc chung

```text
Một class nên có một trách nhiệm rõ ràng.

Không đặt business logic trong Controller/Endpoint.

Không để Domain phụ thuộc EF Core, ASP.NET Core hoặc Infrastructure.

Không tạo cross-service ProjectReference.

Không truy cập trực tiếp database của service khác.

Tên class/method phải diễn đạt đúng ý nghĩa nghiệp vụ.
```
