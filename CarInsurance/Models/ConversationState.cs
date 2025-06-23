namespace CarInsurance.Models;

public record ConversationState(
    Stage CurrentStage = Stage.WaitingPassport,
    byte[]? Passport = null,
    byte[]? VehicleDoc = null
);