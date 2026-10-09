using gui.Model.Managers.MarketManager;
using gui.Model.Persistence;
using gui.Model.Utils;
using System.Diagnostics;
using System.Resources;
using System.Security.AccessControl;
using System.IO;
using Serilog;

namespace gui.Model.Managers.MarketManager
{
    public enum ResourceType
    {
        Coal = 0,
        Oil,
        Garbage,
        Uranium,
    }

    /// <summary>
    /// Singleton manager responsible for handling market resources.
    /// </summary>
    public class MarketManager
    {
        // ====== SINGLETON INSTANCE ======
        private static readonly MarketManager _instance = new();
        public static MarketManager Instance => _instance;

        // ====== PRIVATE FIELDS ======
        private ResourceManager _resourceManager = new();

        // ====== PRIVATE CONSTRUCTOR ======
        private MarketManager() 
        {
            LoadMarketFromCsv();
        }

        // ====== PUBLIC FUNCTIONS ======
        public void Remove(ResourceType resourceType, int quantity = 1)
        {
            for (int i = 0; i < quantity; i++) 
                _resourceManager.MoveToEmpty(resourceType);
        }

        public void Supply(ResourceType resourceType, int quantity = 1)
        {
            for(int i = 0;i < quantity; i++)
                _resourceManager.MoveToFilled(resourceType);
        }

        public void Update(ResourceType resourceType, int quantity)
        {
            if (quantity > 0)
                Supply(resourceType, quantity);
            else
                Remove(resourceType, Math.Abs(quantity));
        }

        public void UpdateMostLimitedResource(int quantity)
        {
            var resourceType = _resourceManager.GetCheapestResourceType();
            Update(resourceType, quantity);
        }


        public void UpdateMostAvaialableResource(int quantity)
        {
            var resourceType = _resourceManager.GetMostExpensiveResourceType();
            Update(resourceType, quantity);
        }

        public int Buy(ResourceType resourceType)
        {
            return _resourceManager.MoveToEmpty(resourceType);
        }

        public bool HasStock(ResourceType resourceType)
        {
            return _resourceManager.HasStock(resourceType);
        }

        public int Sell(ResourceType resourceType)
        {
            return _resourceManager.MoveToFilled(resourceType);
        }

        public IReadOnlyDictionary<int, PriceTier> GetPriceTiers()
        {
            return _resourceManager.GetPriceTiers();
        }

        public void HandleResourceClick(Resource resource)
        {
            _resourceManager.HandleResourceClick(resource);
        }

        public void Reload()
        {
            _resourceManager.Clear();
            // Reload the market configuration
            LoadMarketFromCsv();
        }

        public List<MarketPileSnapshot> ExportPiles() => _resourceManager.ExportPiles();

        public void ImportPiles(IEnumerable<MarketPileSnapshot> piles) => _resourceManager.ImportPiles(piles);

        // 4. Private Methods
        public void LoadMarketFromCsv()
        {
            // Path to the CSV file in the Assets folder
            string csvPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Market.csv");

            Log.Information("csvPath: {csvPath}", csvPath);

            // Read all lines from the CSV file
            var lines = File.ReadAllLines(csvPath);

            // Parse header row for price tiers
            var header = lines[0].Split(',');
            var priceTiers = header.Skip(1)
                .Select((value, index) => new { Price = int.TryParse(value, out var price) ? price : (int?)null, Index = index })
                .Where(x => x.Price.HasValue)
                .ToList();

            // Process each resource row
            foreach (var line in lines.Skip(1))
            {
                var columns = line.Split(',');
                var resourceTypeString = columns[0];
                ResourceType resourceType = Enum.Parse<ResourceType>(resourceTypeString, true);

                for (int i = 1; i < columns.Length; i++)
                {
                    var price = priceTiers.FirstOrDefault(pt => pt.Index == i - 1)?.Price;
                    if (!price.HasValue) continue;

                    // Assume missing or empty values are 0
                    var quantity = string.IsNullOrWhiteSpace(columns[i]) ? 0 : int.Parse(columns[i]);

                    for (int j = 0; j < quantity; j++)
                    {
                        _resourceManager.Create(price.Value, resourceType);
                    }
                }
            }
        }
    }
}

