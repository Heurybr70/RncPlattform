using System;

namespace RncPlatform.Contracts.Responses;

public record AuthResponse(
    string Token, 
    string Username, 
    DateTime ExpiresAt);
