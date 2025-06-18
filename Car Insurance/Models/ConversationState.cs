namespace Car_Insurance.Models;

public record ConversationState(
    Stage CurrentStage = Stage.WaitingPassport,
    byte[]? Passport = null,
    byte[]? VehicleDoc = null
);