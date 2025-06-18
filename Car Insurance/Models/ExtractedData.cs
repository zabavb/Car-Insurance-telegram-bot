namespace Car_Insurance.Models;

public class ExtractedData(string passportName, string passportSurname, string passportId, string vehicleId)
{
    public string PassportName { get; set; } = passportName;
    public string PassportSurname { get; set; } = passportSurname;
    public string PassportId { get; set; } = passportId;
    public string VehicleId { get; set; } = vehicleId;
}