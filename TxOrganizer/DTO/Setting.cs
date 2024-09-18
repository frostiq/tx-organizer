namespace TxOrganizer.DTO;

public enum SettingType
{
    LocationSourceMapping,
}

public class Setting
{
    public SettingType Type { get; set; }
    
    public string Key { get; set; }
    
    public string Value { get; set; }
}