# BFD - DUANCHAMCONG

Sơ đồ Block Flow Diagram (BFD) mô tả tầng Controllers, Services, Data và Models.

```mermaid
flowchart LR
  %% Actors
  Browser[Browser / Client]
  API[HTTP API]

  %% Controllers
  subgraph Controllers [Controllers]
    HomeController[HomeController]
    DashboardController[DashboardController]
    UserController[UserController]
    AttendanceController[AttendanceController]
    LeaveRequestController[LeaveRequestController]
    AuthController[AuthController]
    ChatbotController[ChatbotController]
    ProfileController[ProfileController]
  end

  %% Services
  subgraph Services [Services]
    GeminiService[GeminiService]
    LocalChatbotService[LocalChatbotService]
    AutoCheckoutService[AutoCheckoutService (Hosted)]
  end

  %% Data layer
  ApplicationDbContext[ApplicationDbContext]
  Postgres[(Postgres DB)]

  %% Models & Helpers
  subgraph Models [Domain Models]
    UserModel[User]
    AttendanceModel[Attendance]
    LeaveRequestModel[LeaveRequest]
    ChatHistoryModel[ChatHistory]
    SchoolConfigModel[SchoolConfig]
  end

  subgraph Helpers [Helpers]
    TimeHelper[TimeHelper]
    AttendanceStatus[AttendanceStatus]
  end

  %% Views & Static
  Views[Views (Razor Pages / MVC)]
  StaticFiles[wwwroot / static assets]

  %% Flows
  Browser -->|HTTP| API
  API --> HomeController
  API --> DashboardController
  API --> UserController
  API --> AttendanceController
  API --> LeaveRequestController
  API --> AuthController
  API --> ChatbotController
  API --> ProfileController

  HomeController --> Views
  DashboardController --> Views
  UserController --> Views

  AttendanceController --> ApplicationDbContext
  UserController --> ApplicationDbContext
  LeaveRequestController --> ApplicationDbContext
  ProfileController --> ApplicationDbContext
  AuthController --> ApplicationDbContext
  ChatbotController --> LocalChatbotService

  LocalChatbotService --> ApplicationDbContext
  GeminiService --> ApplicationDbContext
  AutoCheckoutService --> ApplicationDbContext

  ApplicationDbContext --> Postgres

  ApplicationDbContext --> UserModel
  ApplicationDbContext --> AttendanceModel
  ApplicationDbContext --> LeaveRequestModel
  ApplicationDbContext --> ChatHistoryModel

  Controllers --> Helpers
  Services --> Helpers

  API --> StaticFiles
  Views --> StaticFiles

  classDef controllers fill:#f9f,stroke:#333,stroke-width:1px;
  classDef services fill:#bbf,stroke:#333,stroke-width:1px;
  class Controllers controllers;
  class Services services;
```

**Ghi chú:**
- DB: kết nối Npgsql (Postgres) theo `Program.cs`.
- Background service: `AutoCheckoutService` chạy như Hosted Service.
- Các service `GeminiService` và `LocalChatbotService` được đăng ký DI trong `Program.cs`.
