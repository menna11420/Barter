namespace BarterApi.DTOs;

// ===== Auth =====
public record RegisterRequest(string Name, string Email, string Password);
public record LoginRequest(string Email, string Password);
public record GoogleLoginRequest(string IdToken);
public record ResetPasswordRequest(string Email);
public record SendOtpRequest(string UserId);
public record VerifyOtpRequest(string UserId, string Code);
public record ToggleMfaRequest(bool Enabled);

public record AuthResponse(string Token, UserDto User);

// ===== User =====
public record UserDto(
    string Id,
    string Name,
    string Email,
    string? PhotoUrl,
    string? Phone,
    string? Location,
    bool EmailVerified,
    bool MfaEnabled,
    string MfaMethod,
    double RatingSum,
    int ReviewCount,
    double AverageRating,
    DateTime CreatedAt
);

public record UpdateUserRequest(
    string? Name,
    string? PhotoUrl,
    string? Phone,
    string? Location
);

// ===== Items =====
public record CreateItemRequest(
    string Title,
    string Description,
    List<string> ImageUrls,
    int Category,
    int Condition,
    string? PreferredExchange,
    string Location,
    double? Latitude,
    double? Longitude,
    string? DetailedAddress,
    int ItemType,
    bool IsRemote
);

public record UpdateItemRequest(
    string? Title,
    string? Description,
    List<string>? ImageUrls,
    int? Category,
    int? Condition,
    string? PreferredExchange,
    string? Location,
    double? Latitude,
    double? Longitude,
    string? DetailedAddress,
    bool? IsAvailable,
    int? ItemType,
    bool? IsRemote
);

public record ItemDto(
    string Id,
    string OwnerId,
    string OwnerName,
    string Title,
    string Description,
    List<string> ImageUrls,
    int Category,
    int Condition,
    string? PreferredExchange,
    string Location,
    double? Latitude,
    double? Longitude,
    string? DetailedAddress,
    DateTime CreatedAt,
    bool IsAvailable,
    bool IsExchanged,
    int ItemType,
    bool IsRemote
);

// ===== Chats =====
public record CreateChatRequest(string OtherUserId, string ItemId, string ItemTitle);

public record ChatDto(
    string ChatId,
    List<string> Participants,
    string ItemId,
    string ItemTitle,
    string LastMessage,
    DateTime LastMessageTime,
    string LastSenderId,
    int UnreadCount,
    List<string> BlockedBy
);

// ===== Messages =====
public record SendMessageRequest(string Content);

public record MessageDto(
    string MessageId,
    string ChatId,
    string SenderId,
    string Content,
    string MessageType,
    string? PhotoUrl,
    bool IsRead,
    DateTime Timestamp
);

// ===== Exchanges =====
public record ExchangeItemDto(string ItemId, string Title, string ImageUrl);

public record CreateExchangeRequest(
    string ProposedTo,
    List<ExchangeItemDto> ItemsOffered,
    List<ExchangeItemDto> ItemsRequested,
    string? Notes
);

public record UpdateMeetingRequest(string? Location, DateTime? Date);
public record RateExchangeRequest(double Rating, string? Review);

public record ExchangeDto(
    string Id,
    int Status,
    string ProposedBy,
    string ProposedTo,
    List<ExchangeItemDto> ItemsOffered,
    List<ExchangeItemDto> ItemsRequested,
    DateTime ProposedAt,
    DateTime? AcceptedAt,
    DateTime? CompletedAt,
    List<string> ConfirmedBy,
    string? MeetingLocation,
    DateTime? MeetingDate,
    string? Notes,
    string ChatId,
    double? RatingByProposer,
    double? RatingByAccepter,
    string? ReviewByProposer,
    string? ReviewByAccepter
);

// ===== Notifications =====
public record NotificationDto(
    string Id,
    string UserId,
    string Title,
    string Body,
    int Type,
    string? RelatedId,
    bool IsRead,
    DateTime CreatedAt
);

// ===== Reviews =====
public record ReviewDto(
    string Id,
    string ReviewerId,
    string RevieweeId,
    string ExchangeId,
    double Rating,
    string Comment,
    DateTime CreatedAt
);

public record SubmitReviewRequest(
    string ExchangeId,
    string RevieweeId,
    double Rating,
    string Comment
);
