using TxOrganizer.DTO;

namespace TxOrganizer.Database;

public class SettingsRepository
{
    private readonly AppDbContext _dbContext;

    public SettingsRepository(AppDbContext dbContext)
    {
        this._dbContext = dbContext;
    }
    
    // Get setting value by key
    public IEnumerable<Setting> GetSettings(SettingType type)
    {
        var settings = _dbContext.Settings.Where(s => s.Type == type).ToList();
        return settings;
    }
}