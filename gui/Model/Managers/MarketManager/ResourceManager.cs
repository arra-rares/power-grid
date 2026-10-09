using gui.Model.Persistence;
using gui.Model.Utils;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Resources;
using System.Text;
using System.Threading.Tasks;

namespace gui.Model.Managers.MarketManager
{
    public class ResourceManager
    {
        // 1. Fields
        private List<LinkedList<Resource>> _empty = 
            ListUtils.EnumToList<ResourceType, LinkedList<Resource>>(_ => []);

        private List<LinkedList<Resource>> _filled =
            ListUtils.EnumToList<ResourceType, LinkedList<Resource>>(_ => []);

        // 2. Public Methods
        public void Create(int price, ResourceType type)
        {
            // Build resource
            var id = _empty[(int)type].Count;
            // Add it to empty
            _empty[(int)type].AddLast(new Resource(type, price, id));
        }

        public void Clear()
        {
            // Clear all fields
            _empty = ListUtils.EnumToList<ResourceType, LinkedList<Resource>>(_ => []);
            _filled = ListUtils.EnumToList<ResourceType, LinkedList<Resource>>(_ => []);
        }

        public ResourceType GetCheapestResourceType()
        {
            var cheapest = PricesOnMarket().OrderBy(item => item.Price).ThenBy(item => item.Type).First();
            Log.Information("Cheapest resource: {Resource} at {Price}", cheapest.Type, cheapest.Price);
            return cheapest.Type;
        }

        public ResourceType GetMostExpensiveResourceType()
        {
            var expensive = PricesOnMarket().OrderByDescending(item => item.Price).ThenBy(item => item.Type).First();
            Log.Information("Most expensive resource: {Resource} at {Price}", expensive.Type, expensive.Price);
            return expensive.Type;
        }

        private List<(ResourceType Type, int Price)> PricesOnMarket()
        {
            var prices = new List<(ResourceType Type, int Price)>();
            foreach (ResourceType type in Enum.GetValues<ResourceType>())
            {
                var onMarket = _filled[(int)type].First;
                if (onMarket == null)
                    continue;

                prices.Add((type, onMarket.Value.Price));
            }

            if (prices.Count == 0)
                throw new InvalidOperationException("No resource is on the market.");

            return prices;
        }


        public IReadOnlyDictionary<int, PriceTier> GetPriceTiers()
        {
            Dictionary<int, PriceTier> result = [];

            foreach (ResourceType type in Enum.GetValues<ResourceType>())
            {
                var resources = _empty[(int)type]
                    .Concat(_filled[(int)type])
                    .OrderBy(resource => resource.Id);

                foreach (var resource in resources)
                {
                    result.TryAdd(resource.Price, new PriceTier(resource.Price));
                    result[resource.Price].Add(resource);
                }
            }

            return result;
        }

        public void HandleResourceClick(Resource resource)
        {
            if (resource.Filled)
                ProcessFilled(resource);
            else
                ProcessEmpty(resource);
        }


        public int MoveToEmpty(ResourceType resourceType)
        {
            var current = _filled[(int)resourceType].First;
            if (current == null)
                return 0;

            var price = current.Value.Price;

            current.Value.Filled = false;
            _filled[(int)resourceType].Remove(current);
            _empty[(int)resourceType].AddLast(current.Value);


            return price;
        }

        public int MoveToFilled(ResourceType resourceType)
        {
            var current = _empty[(int)resourceType].Last;
            if( current == null)
                return 0;

            var price = current.Value.Price;

            current.Value.Filled = true;
            _empty[(int)resourceType].Remove(current); // Safely remove current
            _filled[(int)resourceType].AddFirst(current.Value);


            return price;
        }

        public bool HasStock(ResourceType resourceType)
        {
            return _filled[(int)resourceType].Count > 0;
        }

        public List<MarketPileSnapshot> ExportPiles()
        {
            var piles = new List<MarketPileSnapshot>();
            foreach (ResourceType type in Enum.GetValues<ResourceType>())
            {
                piles.Add(new MarketPileSnapshot
                {
                    Type = type.ToString(),
                    Empty = Walk(_empty[(int)type]),
                    Filled = Walk(_filled[(int)type])
                });
            }
            return piles;
        }

        public void ImportPiles(IEnumerable<MarketPileSnapshot> piles)
        {
            Clear();
            foreach (var pile in piles)
            {
                if (!Enum.TryParse<ResourceType>(pile.Type, out var type))
                    continue;

                foreach (var token in pile.Empty)
                    _empty[(int)type].AddLast(new Resource(type, token.Price, token.Id));

                foreach (var token in pile.Filled)
                {
                    var resource = new Resource(type, token.Price, token.Id) { };
                    resource.Filled = true;
                    _filled[(int)type].AddLast(resource);
                }
            }
        }

        private static List<TokenSnapshot> Walk(LinkedList<Resource> list)
        {
            var tokens = new List<TokenSnapshot>();
            for (var node = list.First; node != null; node = node.Next)
                tokens.Add(new TokenSnapshot { Id = node.Value.Id, Price = node.Value.Price });
            return tokens;
        }

        // 3. Private Methods
        private void ProcessEmpty(Resource resource)
        {
            var found = _empty[(int)resource.Type].Find(resource);
            if (found == null)
                return;

            var current = _empty[(int)resource.Type].Last;

            while (current != null)
            {
                current.Value.Filled = true; // Change CurrentState
                _filled[(int)resource.Type].AddFirst(current.Value); // Add to filled
                var prev = current.Previous; // Save the reference to the previous node
                _empty[(int)resource.Type].Remove(current); // Safely remove current
                if (current == found) // Stop when we reach 'found'
                    break;
                current = prev; // Move to the previous node
            }
        }

        private void ProcessFilled(Resource resource)
        {
            var found = _filled[(int)resource.Type].Find(resource);
            if (found == null)
                return;

            var current = _filled[(int)resource.Type].First;
            while (current != null)
            {
                current.Value.Filled = false; // Change CurrentState
                _empty[(int)resource.Type].AddLast(current.Value); // Add to filled
                var next = current.Next; // Save the reference to next 
                _filled[(int)resource.Type].Remove(current); // Safely remove current
                if (current == found)
                    break;
                current = next; // Move to next Node   
            }
        }
    }
}
