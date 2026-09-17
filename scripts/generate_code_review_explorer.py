import os
import json
import html
import sys

sys.stdout.reconfigure(encoding='utf-8')

# Directory of the repo
ROOT_DIR = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))

# Metadata definition for all 40 files
FILE_METADATA = [
    {
        "path": "src/BuildingBlocks/ToeicSpace.BuildingBlocks/Pagination/PaginatedResult.cs",
        "layer": "BuildingBlocks",
        "title": "PaginatedResult<T> Generic Wrapper",
        "purpose": "Mô hình chuẩn hóa kết quả phân trang (pagination) cho toàn bộ hệ thống TOEIC Space.",
        "highlights": "Sử dụng Generic Type <T> với thuộc tính read-only và tính toán tự động TotalPages, HasPreviousPage, HasNextPage. Giúp mọi truy vấn danh sách (tests, questions, courses) có chung cấu trúc JSON trả về cho Frontend.",
        "connections": "Được sử dụng bởi GetQuestionsQuery, GetTestsQuery và các Controllers."
    },
    {
        "path": "src/BuildingBlocks/ToeicSpace.BuildingBlocks.Messaging/IIntegrationEvent.cs",
        "layer": "BuildingBlocks",
        "title": "IIntegrationEvent Contract Marker",
        "purpose": "Interface hợp đồng cơ sở cho mọi sự kiện tích hợp (Integration Events) trong hệ thống SOA.",
        "highlights": "Đảm bảo tính nhất quán của tất cả các event được phát qua Message Broker (RabbitMQ): bắt buộc có Id (Guid) duy nhất và thời gian phát sinh OccurredOn (UTC).",
        "connections": "Kế thừa bởi ExamAttemptSubmittedIntegrationEvent, QuestionCreatedIntegrationEvent..."
    },
    {
        "path": "src/BuildingBlocks/ToeicSpace.BuildingBlocks.Messaging/IIntegrationEventPublisher.cs",
        "layer": "BuildingBlocks",
        "title": "IIntegrationEventPublisher Interface",
        "purpose": "Hợp đồng trừu tượng hóa việc phát tán sự kiện lên Message Bus theo nguyên lý DIP.",
        "highlights": "Cho phép tầng Application phát event mà không cần phụ thuộc trực tiếp vào thư viện MassTransit hay RabbitMQ cụ thể. Cực kỳ thuận lợi khi viết Unit Tests (Mock publisher).",
        "connections": "Được inject vào các Command Handlers và được triển khai bởi MassTransitEventPublisher."
    },
    {
        "path": "src/BuildingBlocks/ToeicSpace.BuildingBlocks.Messaging/Events/ExamAttemptSubmittedIntegrationEvent.cs",
        "layer": "BuildingBlocks",
        "title": "ExamAttemptSubmittedIntegrationEvent (Hợp Đồng Bắt Tay)",
        "purpose": "Hợp đồng bất đồng bộ phát ra khi học viên hoàn thành một lượt làm bài thi.",
        "highlights": "Mang theo toàn bộ kết quả thi: Điểm Listening (0-495), Điểm Reading (0-495), Tổng điểm (0-990), Số câu đúng, Thời gian làm bài. Được các service khác (Classroom để cập nhật bảng vàng, Learning để phân tích điểm yếu) lắng nghe độc lập qua RabbitMQ.",
        "connections": "Assessment publish -> Classroom & Learning services consume."
    },
    {
        "path": "src/BuildingBlocks/ToeicSpace.BuildingBlocks.Messaging/Events/QuestionCreatedIntegrationEvent.cs",
        "layer": "BuildingBlocks",
        "title": "QuestionCreatedIntegrationEvent (Hợp Đồng Bắt Tay)",
        "purpose": "Hợp đồng bất đồng bộ phát ra khi có câu hỏi mới được thêm vào ngân hàng đề thi.",
        "highlights": "Cung cấp QuestionId, TestId, Part, Section, Cấp độ khó cho các dịch vụ tìm kiếm và lập chỉ mục (Search/Elasticsearch/AI indexing).",
        "connections": "Được phát từ CreateQuestionCommandHandler."
    },
    {
        "path": "src/BuildingBlocks/ToeicSpace.BuildingBlocks.Messaging/MassTransit/MassTransitEventPublisher.cs",
        "layer": "BuildingBlocks",
        "title": "MassTransitEventPublisher Implementation",
        "purpose": "Lớp thực thi IIntegrationEventPublisher sử dụng IPublishEndpoint của MassTransit.",
        "highlights": "Tự động phân phối message đến đúng RabbitMQ exchange tương ứng với kiểu generic type TEvent.",
        "connections": "Triển khai IIntegrationEventPublisher, gọi IPublishEndpoint.Publish."
    },
    {
        "path": "src/BuildingBlocks/ToeicSpace.BuildingBlocks.Messaging/MassTransit/DependencyInjection.cs",
        "layer": "BuildingBlocks",
        "title": "Messaging Bus DependencyInjection",
        "purpose": "Đăng ký MassTransit và cấu hình kết nối RabbitMQ dùng chung.",
        "highlights": "Cơ chế Fallback thông minh: Tự động kết nối RabbitMQ nếu có cấu hình Host, và tự động chuyển sang In-Memory Bus nếu chạy offline hoặc môi trường Unit Test, giúp hệ thống không bao giờ bị crash khi thiếu broker.",
        "connections": "Được gọi trong Infrastructure DependencyInjection của các service."
    },
    {
        "path": "src/Services/Assessment/ToeicSpace.Assessment.Domain/Common/BaseAuditableEntity.cs",
        "layer": "Domain",
        "title": "BaseAuditableEntity",
        "purpose": "Lớp cơ sở (Base Class) cho tất cả các thực thể (Entities) trong Assessment bounded context.",
        "highlights": "Cung cấp trường khóa chính Id (Guid) và 2 mốc thời gian CreatedAt, UpdatedAt (UTC). Đảm bảo tính nhất quán và truy vết lịch sử dữ liệu chuẩn DDD.",
        "connections": "Kế thừa bởi ToeicTest, ToeicPassage, ToeicQuestion, ToeicAttempt."
    },
    {
        "path": "src/Services/Assessment/ToeicSpace.Assessment.Domain/Entities/ToeicTest.cs",
        "layer": "Domain",
        "title": "ToeicTest Entity",
        "purpose": "Thực thể đại diện cho một bộ đề thi TOEIC (Full Test 200 câu hoặc đề luyện tập theo Part).",
        "highlights": "Lưu trữ metadata toàn diện: Mã đề (Code), Tiêu đề (Title), Năm (Year), Danh mục (Category), Thời lượng (DurationMinutes), Tổng số câu hỏi từng phần (Listening/Reading), Audio tổng và liên kết 1-Nhiều với ToeicPassage và ToeicQuestion.",
        "connections": "Chứa Navigation Collections: Passages, Questions, Attempts."
    },
    {
        "path": "src/Services/Assessment/ToeicSpace.Assessment.Domain/Entities/ToeicPassage.cs",
        "layer": "Domain",
        "title": "ToeicPassage Entity",
        "purpose": "Thực thể đại diện cho bài đọc hiểu (Part 6, Part 7) hoặc đoạn hội thoại/bài nói (Part 3, Part 4).",
        "highlights": "Hỗ trợ đa phương tiện: Content (văn bản bài đọc), AudioUrl (file nghe CDN R2), ImageUrl (hình ảnh bài thi), Transcript và thứ tự hiển thị OrderIndex. Liên kết 1-Nhiều với danh sách câu hỏi con của đoạn văn.",
        "connections": "Thuộc về ToeicTest và chứa collection Questions con."
    },
    {
        "path": "src/Services/Assessment/ToeicSpace.Assessment.Domain/Entities/ToeicQuestion.cs",
        "layer": "Domain",
        "title": "ToeicQuestion Entity",
        "purpose": "Thực thể câu hỏi TOEIC - trung tâm của ngân hàng câu hỏi và phòng thi.",
        "highlights": "Tuân thủ đặc tả ETS: Hỗ trợ 4 phương án A, B, C, D (riêng Part 2 chỉ 3 options A, B, C với D=null), đáp án đúng (CorrectAnswer), giải thích chi tiết (Explanation), lời thoại (Transcript), cấp độ khó (DifficultyLevel), cơ chế Versioning kiểm soát thay đổi và DeletedAt phục vụ Soft Delete an toàn dữ liệu.",
        "connections": "Liên kết trực tiếp tới ToeicTest hoặc ToeicPassage; được truy vấn bởi Question Queries."
    },
    {
        "path": "src/Services/Assessment/ToeicSpace.Assessment.Domain/Entities/ToeicAttempt.cs",
        "layer": "Domain",
        "title": "ToeicAttempt Entity",
        "purpose": "Thực thể lưu phiên làm bài thi thử hoặc bài luyện tập của học viên.",
        "highlights": "Ghi nhận UserId, TestId, AttemptMode (FullTest / Practice), thời gian bắt đầu (StartedAt), thời gian nộp bài (CompletedAt), tổng điểm Listening/Reading/Overall và trạng thái bài nộp.",
        "connections": "Thuộc về ToeicTest và chứa danh sách các câu trả lời ToeicAttemptAnswer."
    },
    {
        "path": "src/Services/Assessment/ToeicSpace.Assessment.Domain/Entities/ToeicAttemptAnswer.cs",
        "layer": "Domain",
        "title": "ToeicAttemptAnswer Entity",
        "purpose": "Thực thể chi tiết từng câu trả lời của học viên trong một lượt làm bài.",
        "highlights": "Lưu QuestionId, phương án học viên chọn (SelectedOption: A/B/C/D), cờ IsCorrect để chấm điểm tự động và thời gian trả lời TimeSpentSeconds.",
        "connections": "Thuộc về ToeicAttempt và tham chiếu tới ToeicQuestion."
    },
    {
        "path": "src/Services/Assessment/ToeicSpace.Assessment.Domain/Enums/ToeicPart.cs",
        "layer": "Domain",
        "title": "ToeicPart Enum",
        "purpose": "Định nghĩa 7 phần thi chuẩn TOEIC Listening & Reading.",
        "highlights": "Part 1 (Mô tả tranh), Part 2 (Hỏi - Đáp), Part 3 (Đoạn hội thoại), Part 4 (Bài nói ngắn), Part 5 (Điền câu), Part 6 (Điền đoạn văn), Part 7 (Đọc hiểu).",
        "connections": "Được dùng trong Question, Passage, DTOs và bộ lọc API."
    },
    {
        "path": "src/Services/Assessment/ToeicSpace.Assessment.Domain/Enums/ToeicSection.cs",
        "layer": "Domain",
        "title": "ToeicSection Enum",
        "purpose": "Phân chia 2 kỹ năng cốt lõi của bài thi TOEIC: Listening (1) và Reading (2).",
        "highlights": "Dùng để phân luồng tính điểm Listening (tối đa 495) và Reading (tối đa 495).",
        "connections": "Được ánh xạ trong Question, Test và DTOs."
    },
    {
        "path": "src/Services/Assessment/ToeicSpace.Assessment.Domain/Enums/ContentStatus.cs",
        "layer": "Domain",
        "title": "ContentStatus Enum",
        "purpose": "Kiểm soát vòng đời phát hành nội dung (Draft = 0, Active = 1, Archived = 2).",
        "highlights": "Cho phép biên soạn đề thi ở chế độ Draft, phát hành công khai khi Active và lưu trữ ẩn khi Archived.",
        "connections": "Được kiểm soát thông qua API PATCH /status và các bộ lọc tìm kiếm."
    },
    {
        "path": "src/Services/Assessment/ToeicSpace.Assessment.Domain/Enums/QuestionDifficulty.cs",
        "layer": "Domain",
        "title": "QuestionDifficulty Enum",
        "purpose": "Đánh giá cấp độ câu hỏi từ 1 đến 5 (VeryEasy, Easy, Medium, Hard, VeryHard).",
        "highlights": "Phục vụ cho thuật toán gợi ý luyện tập thông minh và lọc câu hỏi theo trình độ mục tiêu (350+, 600+, 800+).",
        "connections": "Thuộc tính trong ToeicQuestion và lọc trong GetQuestionsQuery."
    },
    {
        "path": "src/Services/Assessment/ToeicSpace.Assessment.Domain/Exceptions/ErrorType.cs",
        "layer": "Domain",
        "title": "ErrorType Enum",
        "purpose": "Phân loại lỗi nghiệp vụ trong Domain (Validation, NotFound, Conflict, Forbidden, Unauthenticated).",
        "highlights": "Tách biệt mã lỗi ứng dụng khỏi tầng HTTP web, giúp domain độc lập hoàn toàn với ASP.NET Core.",
        "connections": "Được sử dụng bởi AppException và GlobalExceptionHandler."
    },
    {
        "path": "src/Services/Assessment/ToeicSpace.Assessment.Domain/Exceptions/ErrorCodes.cs",
        "layer": "Domain",
        "title": "ErrorCodes Constants",
        "purpose": "Tập hợp các hằng số mã lỗi chuẩn cho frontend (e.g. not_found, validation_error).",
        "highlights": "Giúp Client (React/Vue/Mobile) dễ dàng bắt mã lỗi cố định (Machine-readable) thay vì phụ thuộc vào chuỗi text message.",
        "connections": "Nhúng trong RFC 7807 ProblemDetails."
    },
    {
        "path": "src/Services/Assessment/ToeicSpace.Assessment.Domain/Exceptions/AppException.cs",
        "layer": "Domain",
        "title": "AppException Class",
        "purpose": "Lớp ngoại lệ trung tâm cho nghiệp vụ Assessment.",
        "highlights": "Cung cấp static factory methods tiện lợi (AppException.NotFound, AppException.Validation, AppException.Conflict) kèm từ điển lỗi chi tiết từng trường (ValidationErrors).",
        "connections": "Được throw bởi Handlers/Validators và được bắt tự động bởi GlobalExceptionHandler."
    },
    {
        "path": "src/Services/Assessment/ToeicSpace.Assessment.Application/Data/IAssessmentDbContext.cs",
        "layer": "Application",
        "title": "IAssessmentDbContext Interface",
        "purpose": "Interface trừu tượng hóa truy cập Database theo nguyên lý Dependency Inversion (DIP).",
        "highlights": "Tầng Application chỉ phụ thuộc vào interface này mà không phụ thuộc trực tiếp vào EF Core Implementation hay MySQL driver cụ thể. Dễ dàng viết Unit Test Mocking.",
        "connections": "Được triển khai bởi AssessmentDbContext ở tầng Infrastructure."
    },
    {
        "path": "src/Services/Assessment/ToeicSpace.Assessment.Application/Behaviors/ValidationBehavior.cs",
        "layer": "Application",
        "title": "ValidationBehavior Pipeline",
        "purpose": "MediatR Pipeline Behavior tự động xác thực mọi Command/Query bằng FluentValidation.",
        "highlights": "Chặn đứng mọi request không hợp lệ trước khi chạm vào Handler. Tổng hợp toàn bộ lỗi theo tên thuộc tính và ném AppException với mã lỗi 400 Bad Request.",
        "connections": "Tự động kích hoạt trên mọi IRequest trong MediatR pipeline."
    },
    {
        "path": "src/Services/Assessment/ToeicSpace.Assessment.Application/Dtos/TestDto.cs",
        "layer": "Application",
        "title": "TestDto & TestDetailDto",
        "purpose": "Data Transfer Objects trả về danh sách và chi tiết đề thi.",
        "highlights": "Sử dụng C# Record bất biến (Immutable), che giấu các trường nhạy cảm hoặc không cần thiết, tối ưu hóa băng thông truyền tải JSON.",
        "connections": "Dùng trong GetTestsQuery và GetTestByIdQuery."
    },
    {
        "path": "src/Services/Assessment/ToeicSpace.Assessment.Application/Dtos/QuestionDto.cs",
        "layer": "Application",
        "title": "QuestionDto & QuestionDetailDto",
        "purpose": "Data Transfer Objects phục vụ xem danh sách và chi tiết câu hỏi.",
        "highlights": "QuestionDto trả về thông tin cơ bản cho danh sách bảng; QuestionDetailDto bổ sung đáp án đúng, giải thích, transcript và trạng thái AI Explanation.",
        "connections": "Dùng trong GetQuestionsQuery, GetQuestionByIdQuery, Create/Update commands."
    },
    {
        "path": "src/Services/Assessment/ToeicSpace.Assessment.Application/Dtos/PassageDto.cs",
        "layer": "Application",
        "title": "PassageDto",
        "purpose": "Data Transfer Object cho đoạn văn bài đọc hoặc bài nghe.",
        "highlights": "Gồm tiêu đề, nội dung, audio, hình ảnh và transcript hỗ trợ render giao diện làm bài.",
        "connections": "Dùng trong các truy vấn đề thi và quản lý nội dung."
    },
    {
        "path": "src/Services/Assessment/ToeicSpace.Assessment.Application/Dtos/FullTestDto.cs",
        "layer": "Application",
        "title": "FullTestDto & Cấu Trúc Phân Cấp Phòng Thi",
        "purpose": "DTO cao cấp phân cấp toàn diện đề thi (Test -> Parts 1..7 -> Passages & Questions).",
        "highlights": "Cấu trúc thiết kế chuyên biệt cho màn hình thi trực tuyến. Tự động gom nhóm câu hỏi theo đoạn văn hoặc câu độc lập, hỗ trợ bật/tắt đáp án (includeAnswers) theo chế độ thi thử hoặc xem giải.",
        "connections": "Được trả về bởi GetFullTestQuery và TestsController."
    },
    {
        "path": "src/Services/Assessment/ToeicSpace.Assessment.Application/Questions/Queries/GetQuestionsQuery.cs",
        "layer": "Application",
        "title": "GetQuestionsQuery & Handler",
        "purpose": "Truy vấn danh sách câu hỏi có phân trang và bộ lọc chuyên sâu.",
        "highlights": "Sử dụng EF Core AsNoTracking() tối đa hiệu năng. Cho phép lọc linh hoạt theo TestId, PassageId, Part, Section, Độ khó, Trạng thái và tìm kiếm full-text.",
        "connections": "Gọi từ GET /api/v1/questions, trả về PaginatedResult<QuestionDto>."
    },
    {
        "path": "src/Services/Assessment/ToeicSpace.Assessment.Application/Questions/Queries/GetQuestionByIdQuery.cs",
        "layer": "Application",
        "title": "GetQuestionByIdQuery & Handler",
        "purpose": "Truy vấn chi tiết một câu hỏi theo GUID.",
        "highlights": "Kiểm tra Soft Delete (DeletedAt == null), tự động ném AppException.NotFound nếu không tồn tại.",
        "connections": "Gọi từ GET /api/v1/questions/{id}."
    },
    {
        "path": "src/Services/Assessment/ToeicSpace.Assessment.Application/Questions/Commands/CreateQuestionCommand.cs",
        "layer": "Application",
        "title": "CreateQuestionCommand + Validator + Handler",
        "purpose": "Tạo câu hỏi TOEIC mới vào ngân hàng câu hỏi.",
        "highlights": "Tích hợp CreateQuestionCommandValidator kiểm tra quy tắc ETS: Option A, B, C bắt buộc, Option D bắt buộc với các Part khác Part 2, CorrectAnswer phải thuộc [A, B, C, D]. Tự động khởi tạo Version = 1.",
        "connections": "Gọi từ POST /api/v1/questions."
    },
    {
        "path": "src/Services/Assessment/ToeicSpace.Assessment.Application/Questions/Commands/UpdateQuestionCommand.cs",
        "layer": "Application",
        "title": "UpdateQuestionCommand + Validator + Handler",
        "purpose": "Cập nhật toàn diện nội dung, phương án và giải thích câu hỏi.",
        "highlights": "Cơ chế Tự động tăng Version (Version += 1) và gán UpdatedAt = DateTime.UtcNow phục vụ Audit Trail và chống xung đột sửa đổi.",
        "connections": "Gọi từ PUT /api/v1/questions/{id}."
    },
    {
        "path": "src/Services/Assessment/ToeicSpace.Assessment.Application/Questions/Commands/UpdateQuestionStatusCommand.cs",
        "layer": "Application",
        "title": "UpdateQuestionStatusCommand + Validator + Handler",
        "purpose": "Cập nhật trạng thái hiển thị của câu hỏi (Draft, Active, Archived).",
        "highlights": "Triển khai chuẩn HTTP PATCH: Chỉ cập nhật duy nhất thuộc tính Status mà không bắt client gửi lại toàn bộ dữ liệu câu hỏi.",
        "connections": "Gọi từ PATCH /api/v1/questions/{id}/status."
    },
    {
        "path": "src/Services/Assessment/ToeicSpace.Assessment.Application/Questions/Commands/DeleteQuestionCommand.cs",
        "layer": "Application",
        "title": "DeleteQuestionCommand & Handler",
        "purpose": "Xóa mềm câu hỏi khỏi ngân hàng câu hỏi.",
        "highlights": "Thiết lập DeletedAt = DateTime.UtcNow thay vì xóa cứng khỏi DB, bảo toàn lịch sử các bài làm trước đó của học viên.",
        "connections": "Gọi từ DELETE /api/v1/questions/{id}."
    },
    {
        "path": "src/Services/Assessment/ToeicSpace.Assessment.Application/Tests/Queries/GetTestsQuery.cs",
        "layer": "Application",
        "title": "GetTestsQuery & Handler",
        "purpose": "Lấy danh sách đề thi phân trang phục vụ trang Luyện Đề.",
        "highlights": "Lọc theo Category (FULL_TEST, PRACTICE), Năm, Trạng thái, và tìm kiếm từ khóa theo Tiêu đề, Mã đề, Mô tả.",
        "connections": "Gọi từ GET /api/v1/tests."
    },
    {
        "path": "src/Services/Assessment/ToeicSpace.Assessment.Application/Tests/Queries/GetTestByIdQuery.cs",
        "layer": "Application",
        "title": "GetTestByIdQuery & Handler",
        "purpose": "Lấy thông tin tổng quan của một đề thi.",
        "highlights": "Truy vấn đồng thời đếm số lượng đoạn văn (TotalPassages) liên quan để hiển thị tóm tắt trước khi học viên bắt đầu làm bài.",
        "connections": "Gọi từ GET /api/v1/tests/{id}."
    },
    {
        "path": "src/Services/Assessment/ToeicSpace.Assessment.Application/Tests/Queries/GetFullTestQuery.cs",
        "layer": "Application",
        "title": "GetFullTestQuery & Handler",
        "purpose": "Truy vấn toàn bộ đề thi chuẩn bị cho phiên làm bài thi thử.",
        "highlights": "Tải đề thi, tải toàn bộ passages và câu hỏi liên quan, sắp xếp theo OrderIndex và QuestionNumber, tự động phân nhóm vào 7 Parts và liên kết câu hỏi với bài đọc tương ứng.",
        "connections": "Gọi từ GET /api/v1/tests/{id}/full."
    },
    {
        "path": "src/Services/Assessment/ToeicSpace.Assessment.Application/Tests/Queries/GetTestCategoriesQuery.cs",
        "layer": "Application",
        "title": "GetTestCategoriesQuery & Handler",
        "purpose": "Thống kê danh mục đề thi và các năm phát hành.",
        "highlights": "Dùng GroupBy tính toán nhanh số lượng đề trong từng danh mục (vd: PRACTICE có 71 đề, FULL_TEST có 26 đề) và danh sách năm để hiển thị bộ lọc Tab cho frontend.",
        "connections": "Gọi từ GET /api/v1/tests/categories."
    },
    {
        "path": "src/Services/Assessment/ToeicSpace.Assessment.Application/DependencyInjection.cs",
        "layer": "Application",
        "title": "Application DependencyInjection",
        "purpose": "Đăng ký toàn bộ dịch vụ tầng Application vào IServiceCollection.",
        "highlights": "Đăng ký MediatR handlers từ executing assembly, FluentValidation validators và cấu hình ValidationBehavior tự động trong MediatR pipeline.",
        "connections": "Được gọi trong Program.cs của Assessment.API."
    },
    {
        "path": "src/Services/Assessment/ToeicSpace.Assessment.Infrastructure/Persistence/AssessmentDbContext.cs",
        "layer": "Infrastructure",
        "title": "AssessmentDbContext",
        "purpose": "EF Core DbContext quản trị kết nối và mô hình dữ liệu MySQL cho Assessment Service.",
        "highlights": "Triển khai IAssessmentDbContext, cấu hình quan hệ cascade/restrict, chỉ mục khóa ngoại và tự động áp dụng các Entity Configurations từ assembly.",
        "connections": "Được inject vào các Query & Command Handlers."
    },
    {
        "path": "src/Services/Assessment/ToeicSpace.Assessment.Infrastructure/DependencyInjection.cs",
        "layer": "Infrastructure",
        "title": "Infrastructure DependencyInjection",
        "purpose": "Cấu hình kết nối MySQL và đăng ký DbContext.",
        "highlights": "Tự động đọc chuỗi kết nối từ biến môi trường ConnectionStrings__AssessmentDatabase hoặc appsettings, bật EnableRetryOnFailure (3 lần retry), cấu hình DetailedErrors trong môi trường Development.",
        "connections": "Được gọi trong Program.cs của Assessment.API."
    },
    {
        "path": "src/Services/Assessment/ToeicSpace.Assessment.API/Controllers/QuestionsController.cs",
        "layer": "API",
        "title": "QuestionsController",
        "purpose": "REST Controller cung cấp các API quản trị ngân hàng câu hỏi.",
        "highlights": "Chuẩn hóa đầy đủ các REST verbs: GET (lọc/phân trang), GET by ID, POST (tạo mới trả về 201 CreatedAtAction), PUT (cập nhật), PATCH (sửa trạng thái), DELETE (xóa mềm trả về 204 NoContent) kèm Swagger Annotations.",
        "connections": "Giao tiếp trực tiếp với Client và chuyển tiếp request qua IMediator."
    },
    {
        "path": "src/Services/Assessment/ToeicSpace.Assessment.API/Controllers/TestsController.cs",
        "layer": "API",
        "title": "TestsController",
        "purpose": "REST Controller cung cấp các API truy xuất đề thi cho học viên.",
        "highlights": "Cung cấp các endpoint: Lấy danh sách đề phân trang, tổng hợp categories phục vụ UI tabs, lấy thông tin tổng quan đề và tải toàn bộ đề thi phân cấp phục vụ thi trực tuyến.",
        "connections": "Giao tiếp với giao diện Luyện đề của học viên."
    },
    {
        "path": "src/Services/Assessment/ToeicSpace.Assessment.API/Middlewares/GlobalExceptionHandler.cs",
        "layer": "API",
        "title": "GlobalExceptionHandler",
        "purpose": "Xử lý ngoại lệ tập trung chuẩn hóa theo RFC 7807 Problem Details.",
        "highlights": "Bắt AppException để chuyển thành HTTP Status phù hợp (400, 404, 409, 401, 403) và xuất chi tiết lỗi dạng application/problem+json. Bắt ngoại lệ chưa xử lý và trả về 500 an toàn (không lộ stack trace nhạy cảm ra ngoài).",
        "connections": "Đăng ký qua IExceptionHandler trong ASP.NET Core 9 / .NET 10."
    },
    {
        "path": "src/Services/Assessment/ToeicSpace.Assessment.API/DependencyInjection.cs",
        "layer": "API",
        "title": "API DependencyInjection",
        "purpose": "Cấu hình middleware, CORS, Swagger và JSON Serializer.",
        "highlights": "Cấu hình JsonStringEnumConverter (hiển thị enum dạng chuỗi dễ đọc như 'Part1', 'Listening', 'Active'), cấu hình chính sách CORS và tài liệu Swagger OpenAPI.",
        "connections": "Được gọi trong Program.cs."
    },
    {
        "path": "src/Services/Assessment/ToeicSpace.Assessment.API/Program.cs",
        "layer": "API",
        "title": "Program.cs Entrypoint",
        "purpose": "Khởi chạy ứng dụng Web API của service Assessment.",
        "highlights": "Code tinh gọn theo chuẩn Top-Level Statements của .NET 10, phân tách DI rõ ràng: AddApplicationServices, AddInfrastructureServices, AddApiServices, MapControllers.",
        "connections": "Khởi động toàn bộ Assessment Service."
    },
    {
        "path": "src/ApiGateways/ToeicSpace.ApiGateway/appsettings.json",
        "layer": "API",
        "title": "API Gateway YARP Routing Contract",
        "purpose": "Hợp đồng định tuyến đảo ngược (Reverse Proxy) YARP tại cổng vào duy nhất.",
        "highlights": "Định tuyến /identity/... sang identity-cluster và /assessment/... sang assessment-cluster với PathRemovePrefix tự động. Giúp Frontend chỉ cần gọi qua 1 cổng Gateway duy nhất.",
        "connections": "Cổng giao tiếp duy nhất giữa Frontend và toàn bộ cụm microservices phía sau."
    },
    {
        "path": "scripts/seed_assessment_database.py",
        "layer": "Scripts",
        "title": "Database High-Speed Seeder",
        "purpose": "Nạp toàn bộ 97 đề thi, 7,716 passages và 30,572 câu hỏi vào MySQL local.",
        "highlights": "Sử dụng batch insert tối ưu hóa hiệu suất, cơ chế idempotent (ON DUPLICATE KEY UPDATE) an toàn, ánh xạ chính xác các quan hệ cha-con qua UUID.",
        "connections": "Khởi tạo dữ liệu ban đầu cho database toeic_space_assessment."
    },
    {
        "path": "scripts/test_assessment_api.py",
        "layer": "Scripts",
        "title": "Automated API Test Suite",
        "purpose": "Kịch bản kiểm thử tự động toàn diện 11 API endpoints trên server thực.",
        "highlights": "Kiểm tra chu trình hoàn chỉnh: Đọc đề thi, đọc categories, đọc chi tiết, lấy bài thi full 7 parts, lọc câu hỏi, tạo câu hỏi mới (POST), cập nhật (PUT), đổi trạng thái (PATCH), xóa mềm (DELETE) và xác nhận 404.",
        "connections": "Chạy kiểm thử tích hợp (Integration Tests) cho hệ thống."
    }
]

def load_file_content(rel_path):
    full_path = os.path.join(ROOT_DIR, rel_path.replace('/', os.sep))
    if not os.path.exists(full_path):
        return f"// File not found: {rel_path}"
    with open(full_path, 'r', encoding='utf-8', errors='ignore') as f:
        return f.read()

def generate_explorer_html():
    file_records = []
    for item in FILE_METADATA:
        content = load_file_content(item['path'])
        ext = os.path.splitext(item['path'])[1].lower()
        lang = "csharp" if ext == ".cs" else "python" if ext == ".py" else "json" if ext == ".json" else "clike"
        
        file_records.append({
            "path": item['path'],
            "basename": os.path.basename(item['path']),
            "layer": item['layer'],
            "title": item['title'],
            "purpose": item['purpose'],
            "highlights": item['highlights'],
            "connections": item['connections'],
            "lang": lang,
            "lines": len(content.splitlines()),
            "code": content
        })

    json_data = json.dumps(file_records, ensure_ascii=False)

    html_content = f"""<!DOCTYPE html>
<html lang="vi">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>TOEIC Space - Interactive Code Review & Explorer</title>
    <link rel="preconnect" href="https://fonts.googleapis.com">
    <link rel="preconnect" href="https://fonts.gstatic.com" crossorigin>
    <link href="https://fonts.googleapis.com/css2?family=Fira+Code:wght@400;500;600&family=Plus+Jakarta+Sans:wght@400;500;600;700;800&display=swap" rel="stylesheet">
    <link rel="stylesheet" href="https://cdnjs.cloudflare.com/ajax/libs/prism/1.29.0/themes/prism-tomorrow.min.css">
    <link rel="stylesheet" href="https://cdnjs.cloudflare.com/ajax/libs/font-awesome/6.4.0/css/all.min.css">
    <style>
        :root {{
            --bg-primary: #0a0d14;
            --bg-secondary: #101522;
            --bg-tertiary: #161c2d;
            --bg-hover: #1f273d;
            --border: #26304d;
            --border-highlight: #3b82f6;
            --text-main: #e2e8f0;
            --text-muted: #94a3b8;
            --text-dim: #64748b;
            --accent-blue: #3b82f6;
            --accent-cyan: #06b6d4;
            --accent-green: #10b981;
            --accent-purple: #8b5cf6;
            --accent-amber: #f59e0b;
            --accent-rose: #f43f5e;
            --sidebar-width: 380px;
        }}

        * {{
            margin: 0;
            padding: 0;
            box-sizing: border-box;
        }}

        body {{
            font-family: 'Plus Jakarta Sans', -apple-system, BlinkMacSystemFont, sans-serif;
            background-color: var(--bg-primary);
            color: var(--text-main);
            display: flex;
            flex-direction: column;
            height: 100vh;
            overflow: hidden;
        }}

        /* Header */
        header {{
            height: 64px;
            background: linear-gradient(to right, #0d121f, #131929);
            border-bottom: 1px solid var(--border);
            display: flex;
            align-items: center;
            justify-content: space-between;
            padding: 0 24px;
            z-index: 100;
        }}

        .brand {{
            display: flex;
            align-items: center;
            gap: 14px;
        }}

        .brand-logo {{
            width: 38px;
            height: 38px;
            background: linear-gradient(135deg, #2563eb, #06b6d4);
            border-radius: 10px;
            display: flex;
            align-items: center;
            justify-content: center;
            color: white;
            font-size: 18px;
            box-shadow: 0 4px 12px rgba(37, 99, 235, 0.4);
        }}

        .brand-info h1 {{
            font-size: 16px;
            font-weight: 700;
            letter-spacing: -0.3px;
            color: #ffffff;
            display: flex;
            align-items: center;
            gap: 8px;
        }}

        .brand-badge {{
            font-size: 10px;
            font-weight: 700;
            text-transform: uppercase;
            padding: 2px 7px;
            border-radius: 6px;
            background: rgba(59, 130, 246, 0.2);
            color: #60a5fa;
            border: 1px solid rgba(59, 130, 246, 0.4);
        }}

        .brand-info p {{
            font-size: 12px;
            color: var(--text-muted);
        }}

        .header-stats {{
            display: flex;
            align-items: center;
            gap: 16px;
        }}

        .stat-pill {{
            background: var(--bg-tertiary);
            border: 1px solid var(--border);
            border-radius: 20px;
            padding: 6px 14px;
            font-size: 12px;
            display: flex;
            align-items: center;
            gap: 8px;
            color: var(--text-muted);
        }}

        .stat-pill strong {{
            color: #38bdf8;
        }}

        /* App Container */
        .app-container {{
            display: flex;
            flex: 1;
            height: calc(100vh - 64px);
            overflow: hidden;
        }}

        /* Sidebar */
        .sidebar {{
            width: var(--sidebar-width);
            background: var(--bg-secondary);
            border-right: 1px solid var(--border);
            display: flex;
            flex-direction: column;
            overflow: hidden;
        }}

        .search-box {{
            padding: 16px;
            border-bottom: 1px solid var(--border);
        }}

        .search-input-wrapper {{
            position: relative;
            display: flex;
            align-items: center;
        }}

        .search-input-wrapper i {{
            position: absolute;
            left: 12px;
            color: var(--text-dim);
            font-size: 13px;
        }}

        .search-input {{
            width: 100%;
            background: var(--bg-tertiary);
            border: 1px solid var(--border);
            border-radius: 8px;
            padding: 9px 12px 9px 36px;
            font-size: 13px;
            color: var(--text-main);
            outline: none;
            transition: all 0.2s ease;
        }}

        .search-input:focus {{
            border-color: var(--accent-blue);
            box-shadow: 0 0 0 2px rgba(59, 130, 246, 0.2);
        }}

        .tree-container {{
            flex: 1;
            overflow-y: auto;
            padding: 12px 8px;
        }}

        .tree-container::-webkit-scrollbar {{
            width: 6px;
        }}
        .tree-container::-webkit-scrollbar-thumb {{
            background: var(--border);
            border-radius: 3px;
        }}

        .layer-group {{
            margin-bottom: 8px;
        }}

        .layer-header {{
            display: flex;
            align-items: center;
            gap: 8px;
            padding: 7px 10px;
            border-radius: 6px;
            cursor: pointer;
            user-select: none;
            font-size: 12px;
            font-weight: 700;
            color: var(--text-muted);
            text-transform: uppercase;
            letter-spacing: 0.5px;
            transition: background 0.15s;
        }}

        .layer-header:hover {{
            background: var(--bg-tertiary);
            color: var(--text-main);
        }}

        .layer-header i.chevron {{
            font-size: 10px;
            transition: transform 0.2s ease;
        }}

        .layer-header.collapsed i.chevron {{
            transform: rotate(-90deg);
        }}

        .layer-count {{
            margin-left: auto;
            background: rgba(255, 255, 255, 0.06);
            padding: 1px 6px;
            border-radius: 10px;
            font-size: 11px;
            color: var(--text-dim);
        }}

        .file-list {{
            margin-left: 12px;
            padding-left: 8px;
            border-left: 1px dashed var(--border);
        }}

        .file-item {{
            display: flex;
            align-items: center;
            gap: 8px;
            padding: 8px 10px;
            border-radius: 6px;
            cursor: pointer;
            font-size: 13px;
            color: var(--text-muted);
            transition: all 0.15s ease;
            margin-bottom: 2px;
        }}

        .file-item:hover {{
            background: var(--bg-hover);
            color: var(--text-main);
        }}

        .file-item.active {{
            background: rgba(37, 99, 235, 0.15);
            color: #60a5fa;
            font-weight: 600;
            border-left: 3px solid var(--accent-blue);
        }}

        .file-icon {{
            font-size: 13px;
            width: 16px;
            text-align: center;
        }}

        .file-icon.cs {{ color: #10b981; }}
        .file-icon.py {{ color: #eab308; }}

        .file-name {{
            white-space: nowrap;
            overflow: hidden;
            text-overflow: ellipsis;
            flex: 1;
        }}

        .file-lines {{
            font-size: 11px;
            color: var(--text-dim);
        }}

        /* Main Content View */
        .content-view {{
            flex: 1;
            display: flex;
            flex-direction: column;
            overflow: hidden;
            background: var(--bg-primary);
        }}

        /* Overview Hero Box */
        .file-hero {{
            padding: 20px 28px;
            background: var(--bg-secondary);
            border-bottom: 1px solid var(--border);
            display: flex;
            flex-direction: column;
            gap: 14px;
        }}

        .file-hero-top {{
            display: flex;
            align-items: flex-start;
            justify-content: space-between;
        }}

        .file-titles h2 {{
            font-size: 18px;
            font-weight: 700;
            color: #ffffff;
            display: flex;
            align-items: center;
            gap: 10px;
        }}

        .badge-layer {{
            font-size: 11px;
            font-weight: 600;
            padding: 3px 9px;
            border-radius: 6px;
            text-transform: uppercase;
        }}
        .badge-BuildingBlocks {{ background: rgba(139, 92, 246, 0.2); color: #a78bfa; border: 1px solid rgba(139, 92, 246, 0.4); }}
        .badge-Domain {{ background: rgba(16, 185, 129, 0.2); color: #34d399; border: 1px solid rgba(16, 185, 129, 0.4); }}
        .badge-Application {{ background: rgba(59, 130, 246, 0.2); color: #60a5fa; border: 1px solid rgba(59, 130, 246, 0.4); }}
        .badge-Infrastructure {{ background: rgba(245, 158, 11, 0.2); color: #fbbf24; border: 1px solid rgba(245, 158, 11, 0.4); }}
        .badge-API {{ background: rgba(244, 63, 94, 0.2); color: #fb7185; border: 1px solid rgba(244, 63, 94, 0.4); }}
        .badge-Scripts {{ background: rgba(6, 182, 212, 0.2); color: #22d3ee; border: 1px solid rgba(6, 182, 212, 0.4); }}

        .file-path {{
            font-family: 'Fira Code', monospace;
            font-size: 12px;
            color: var(--text-dim);
            margin-top: 4px;
        }}

        .hero-actions {{
            display: flex;
            gap: 10px;
        }}

        .btn-action {{
            background: var(--bg-tertiary);
            border: 1px solid var(--border);
            color: var(--text-main);
            padding: 8px 14px;
            border-radius: 6px;
            font-size: 12px;
            font-weight: 600;
            cursor: pointer;
            display: flex;
            align-items: center;
            gap: 6px;
            transition: all 0.15s;
        }}

        .btn-action:hover {{
            background: var(--bg-hover);
            border-color: var(--accent-blue);
            color: #ffffff;
        }}

        /* Review Explanation Card */
        .review-card {{
            background: linear-gradient(145deg, #131b2e, #101626);
            border: 1px solid #233152;
            border-radius: 10px;
            padding: 16px 20px;
            display: grid;
            grid-template-columns: 1fr 1fr;
            gap: 16px;
        }}

        .review-section {{
            display: flex;
            flex-direction: column;
            gap: 6px;
        }}

        .review-section.full {{
            grid-column: span 2;
        }}

        .review-label {{
            font-size: 11px;
            font-weight: 700;
            text-transform: uppercase;
            letter-spacing: 0.5px;
            color: #93c5fd;
            display: flex;
            align-items: center;
            gap: 6px;
        }}

        .review-text {{
            font-size: 13px;
            line-height: 1.55;
            color: #cbd5e1;
        }}

        /* Code Viewer */
        .code-viewer {{
            flex: 1;
            overflow: auto;
            padding: 0;
            position: relative;
            background: #090c12;
        }}

        .code-viewer pre {{
            margin: 0 !important;
            padding: 20px 24px !important;
            background: transparent !important;
            font-family: 'Fira Code', monospace !important;
            font-size: 13px !important;
            line-height: 1.65 !important;
        }}

        /* Toast notification */
        .toast {{
            position: fixed;
            bottom: 24px;
            right: 24px;
            background: #10b981;
            color: #ffffff;
            padding: 10px 18px;
            border-radius: 8px;
            font-size: 13px;
            font-weight: 600;
            box-shadow: 0 4px 14px rgba(16, 185, 129, 0.4);
            display: none;
            align-items: center;
            gap: 8px;
            z-index: 999;
        }}

        @media (max-width: 900px) {{
            .sidebar {{ width: 280px; }}
            .review-card {{ grid-template-columns: 1fr; }}
            .review-section.full {{ grid-column: span 1; }}
        }}
    </style>
</head>
<body>

    <header>
        <div class="brand">
            <div class="brand-logo"><i class="fa-solid fa-code-merge"></i></div>
            <div class="brand-info">
                <h1>TOEIC Space <span class="brand-badge">Assessment Service</span></h1>
                <p>Kiến trúc Clean Architecture & CQRS - Đánh giá & Ngân hàng đề thi TOEIC</p>
            </div>
        </div>
        <div class="header-stats">
            <div class="stat-pill"><i class="fa-regular fa-file-code"></i> Total Files: <strong id="total-files">40</strong></div>
            <div class="stat-pill"><i class="fa-solid fa-database"></i> Database: <strong>MySQL 9.3</strong></div>
            <div class="stat-pill"><i class="fa-solid fa-vial-circle-check"></i> Tests: <strong>100% Passed</strong></div>
        </div>
    </header>

    <div class="app-container">
        <!-- Sidebar Tree Explorer -->
        <div class="sidebar">
            <div class="search-box">
                <div class="search-input-wrapper">
                    <i class="fa-solid fa-magnifying-glass"></i>
                    <input type="text" id="filterInput" class="search-input" placeholder="Tìm kiếm file hoặc từ khóa...">
                </div>
            </div>

            <div class="tree-container" id="treeContainer">
                <!-- Layers & files injected by JS -->
            </div>
        </div>

        <!-- Main Code & Explanation View -->
        <div class="content-view">
            <div class="file-hero" id="fileHero">
                <div class="file-hero-top">
                    <div class="file-titles">
                        <h2 id="heroTitle">Select a file</h2>
                        <div class="file-path" id="heroPath">src/...</div>
                    </div>
                    <div class="hero-actions">
                        <button class="btn-action" id="copyBtn"><i class="fa-regular fa-copy"></i> Copy Code</button>
                    </div>
                </div>

                <div class="review-card" id="reviewCard">
                    <div class="review-section full">
                        <span class="review-label"><i class="fa-solid fa-bullseye"></i> Mục Đích File Code</span>
                        <p class="review-text" id="reviewPurpose">...</p>
                    </div>
                    <div class="review-section">
                        <span class="review-label"><i class="fa-solid fa-wand-magic-sparkles"></i> Senior Architecture Highlights</span>
                        <p class="review-text" id="reviewHighlights">...</p>
                    </div>
                    <div class="review-section">
                        <span class="review-label"><i class="fa-solid fa-link"></i> Liên Kết Trong Hệ Thống</span>
                        <p class="review-text" id="reviewConnections">...</p>
                    </div>
                </div>
            </div>

            <div class="code-viewer">
                <pre><code id="codeBlock" class="language-csharp">// Code will be displayed here</code></pre>
            </div>
        </div>
    </div>

    <div class="toast" id="toast">
        <i class="fa-solid fa-check"></i> Đã sao chép code vào clipboard!
    </div>

    <!-- Scripts -->
    <script src="https://cdnjs.cloudflare.com/ajax/libs/prism/1.29.0/prism.min.js"></script>
    <script src="https://cdnjs.cloudflare.com/ajax/libs/prism/1.29.0/components/prism-csharp.min.js"></script>
    <script src="https://cdnjs.cloudflare.com/ajax/libs/prism/1.29.0/components/prism-python.min.js"></script>
    <script>
        const filesData = {json_data};
        let currentFileIndex = 0;

        const layerOrder = ["BuildingBlocks", "Domain", "Application", "Infrastructure", "API", "Scripts"];
        const layerIcons = {{
            "BuildingBlocks": "fa-cube",
            "Domain": "fa-landmark",
            "Application": "fa-bolt-lightning",
            "Infrastructure": "fa-server",
            "API": "fa-network-wired",
            "Scripts": "fa-terminal"
        }};

        function renderTree(filterText = "") {{
            const treeContainer = document.getElementById("treeContainer");
            treeContainer.innerHTML = "";

            const query = filterText.toLowerCase().trim();

            layerOrder.forEach(layer => {{
                const layerFiles = filesData.filter(f => f.layer === layer && 
                    (f.basename.toLowerCase().includes(query) || 
                     f.path.toLowerCase().includes(query) ||
                     f.title.toLowerCase().includes(query) ||
                     f.purpose.toLowerCase().includes(query))
                );

                if (layerFiles.length === 0) return;

                const layerGroup = document.createElement("div");
                layerGroup.className = "layer-group";

                const layerHeader = document.createElement("div");
                layerHeader.className = "layer-header";
                layerHeader.innerHTML = `
                    <i class="fa-solid fa-chevron-down chevron"></i>
                    <i class="fa-solid ${{layerIcons[layer] || 'fa-folder'}}"></i>
                    <span>${{layer}}</span>
                    <span class="layer-count">${{layerFiles.length}}</span>
                `;

                const fileList = document.createElement("div");
                fileList.className = "file-list";

                layerHeader.addEventListener("click", () => {{
                    layerHeader.classList.toggle("collapsed");
                    fileList.style.display = layerHeader.classList.contains("collapsed") ? "none" : "block";
                }});

                layerFiles.forEach(file => {{
                    const originalIndex = filesData.findIndex(f => f.path === file.path);
                    const fileItem = document.createElement("div");
                    fileItem.className = `file-item ${{originalIndex === currentFileIndex ? 'active' : ''}}`;
                    fileItem.setAttribute("data-index", originalIndex);

                    const extIcon = file.lang === 'python' ? 'fa-brands fa-python py' : 'fa-solid fa-code cs';

                    fileItem.innerHTML = `
                        <i class="${{extIcon}} file-icon"></i>
                        <span class="file-name" title="${{file.basename}}">${{file.basename}}</span>
                        <span class="file-lines">${{file.lines}}L</span>
                    `;

                    fileItem.addEventListener("click", () => {{
                        selectFile(originalIndex);
                    }});

                    fileList.appendChild(fileItem);
                }});

                layerGroup.appendChild(layerHeader);
                layerGroup.appendChild(fileList);
                treeContainer.appendChild(layerGroup);
            }});
        }}

        function selectFile(index) {{
            currentFileIndex = index;
            const file = filesData[index];

            // Update UI elements
            document.querySelectorAll(".file-item").forEach(el => {{
                el.classList.toggle("active", el.getAttribute("data-index") == index);
            }});

            document.getElementById("heroTitle").innerHTML = `
                ${{file.basename}} 
                <span class="badge-layer badge-${{file.layer}}">${{file.layer}}</span>
            `;
            document.getElementById("heroPath").innerText = file.path;
            document.getElementById("reviewPurpose").innerText = file.purpose;
            document.getElementById("reviewHighlights").innerText = file.highlights;
            document.getElementById("reviewConnections").innerText = file.connections;

            const codeBlock = document.getElementById("codeBlock");
            codeBlock.className = `language-${{file.lang}}`;
            codeBlock.textContent = file.code;

            Prism.highlightElement(codeBlock);
        }}

        // Search Filter
        document.getElementById("filterInput").addEventListener("input", (e) => {{
            renderTree(e.target.value);
        }});

        // Copy button
        document.getElementById("copyBtn").addEventListener("click", () => {{
            const code = filesData[currentFileIndex].code;
            navigator.clipboard.writeText(code).then(() => {{
                const toast = document.getElementById("toast");
                toast.style.display = "flex";
                setTimeout(() => {{
                    toast.style.display = "none";
                }}, 2500);
            }});
        }});

        // Initial render
        renderTree();
        if (filesData.length > 0) {{
            selectFile(0);
        }}
    </script>
</body>
</html>
"""

    out_path = os.path.join(ROOT_DIR, "docs", "CODE_REVIEW_TREE_EXPLORER.html")
    with open(out_path, "w", encoding="utf-8") as f:
        f.write(html_content)

    print(f"Generated successfully: {out_path} ({len(file_records)} files embedded)")

if __name__ == "__main__":
    generate_explorer_html()
