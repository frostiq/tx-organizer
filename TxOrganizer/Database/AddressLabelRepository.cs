using Microsoft.EntityFrameworkCore;
using TxOrganizer.DTO;

namespace TxOrganizer.Database;

public class AddressLabelRepository
{
    private readonly AppDbContext _dbContext;

    public AddressLabelRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

        // Add or update an address label
    public async Task AddOrUpdateLabelAsync(string address, string label, string? category = null)
    {
        var normalizedAddress = address.ToLowerInvariant();
        
        var existingLabel = await _dbContext.AddressLabels
            .FirstOrDefaultAsync(x => x.Address == normalizedAddress);

        if (existingLabel != null)
        {
            // Update existing label
            existingLabel.Label = label;
            existingLabel.Category = category;
        }
        else
        {
            // Add new label
            var addressLabel = new AddressLabel
            {
                Address = normalizedAddress,
                Label = label,
                Category = category
            };
            _dbContext.AddressLabels.Add(addressLabel);
        }

        await _dbContext.SaveChangesAsync();
    }

    // Get label for a specific address
    public async Task<AddressLabel?> GetLabelAsync(string address)
    {
        var normalizedAddress = address.ToLowerInvariant();
        return await _dbContext.AddressLabels
            .FirstOrDefaultAsync(x => x.Address == normalizedAddress);
    }

    // Get all labels
    public async Task<List<AddressLabel>> GetAllLabelsAsync()
    {
        return await _dbContext.AddressLabels
            .OrderBy(x => x.Label)
            .ToListAsync();
    }

    // Get labels by category
    public async Task<List<AddressLabel>> GetLabelsByCategoryAsync(string category)
    {
        return await _dbContext.AddressLabels
            .Where(x => x.Category == category)
            .OrderBy(x => x.Label)
            .ToListAsync();
    }

    // Search labels by name
    public async Task<List<AddressLabel>> SearchLabelsAsync(string searchTerm)
    {
        var lowerSearchTerm = searchTerm.ToLowerInvariant();
        return await _dbContext.AddressLabels
            .Where(x => x.Label.ToLower().Contains(lowerSearchTerm))
            .OrderBy(x => x.Label)
            .ToListAsync();
    }

    // Delete a label
    public async Task DeleteLabelAsync(string address)
    {
        var normalizedAddress = address.ToLowerInvariant();
        var label = await _dbContext.AddressLabels
            .FirstOrDefaultAsync(x => x.Address == normalizedAddress);

        if (label != null)
        {
            _dbContext.AddressLabels.Remove(label);
            await _dbContext.SaveChangesAsync();
        }
    }

    // Get label text for display (returns label if exists, otherwise shortened address)
    public async Task<string> GetDisplayLabelAsync(string address)
    {
        var label = await GetLabelAsync(address);
        if (label != null)
        {
            return label.Label;
        }

        // Return shortened address format
        if (address.Length > 10)
        {
            return $"{address[..6]}...{address[^4..]}";
        }
        return address;
    }

    // Bulk import address labels
    public async Task BulkImportLabelsAsync(IEnumerable<AddressLabel> labels)
    {
        var labelsToAdd = new List<AddressLabel>();
        var labelsToUpdate = new List<AddressLabel>();

        foreach (var label in labels)
        {
            var normalizedAddress = label.Address.ToLowerInvariant();
            
            var existingLabel = await _dbContext.AddressLabels
                .FirstOrDefaultAsync(x => x.Address == normalizedAddress);

            if (existingLabel != null)
            {
                // Update existing
                existingLabel.Label = label.Label;
                existingLabel.Category = label.Category;
                labelsToUpdate.Add(existingLabel);
            }
            else
            {
                // Add new
                labelsToAdd.Add(new AddressLabel
                {
                    Address = normalizedAddress,
                    Label = label.Label,
                    Category = label.Category
                });
            }
        }

        if (labelsToAdd.Any())
        {
            _dbContext.AddressLabels.AddRange(labelsToAdd);
        }

        await _dbContext.SaveChangesAsync();
    }

    // Get categories
    public async Task<List<string>> GetCategoriesAsync()
    {
        return await _dbContext.AddressLabels
            .Where(x => x.Category != null)
            .Select(x => x.Category!)
            .Distinct()
            .OrderBy(x => x)
            .ToListAsync();
    }
}
