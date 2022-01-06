using System.Collections.Generic;
using System.Linq;

#nullable enable

namespace NetProxy.Configuration.Routes
{
    public class RoutesRepository
    {
        private HashSet<RouteMapping> _mappings = new();

        public RoutesRepository()
        {

        }
         
        public RoutesRepository(IEnumerable<RouteMapping> mappings)
        {
            Mappings = new HashSet<RouteMapping>(mappings); 
        }

        public RouteMapping? FirstOrDefault(uint fromId) => Mappings.FirstOrDefault(x => x.Matches(fromId)) ?? DefaultMapping;

        public RouteMapping? DefaultMapping { get; private set; }

        public HashSet<RouteMapping> Mappings
        {
            get => _mappings;
            set
            {
                _mappings = value;
                DefaultMapping = Mappings.FirstOrDefault(x => x.IsManyToOne);
            }
        }
    }
}