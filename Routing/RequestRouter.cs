using NetProxy.Configuration.Routes;
using NetProxy.Parser;
using System.IO;

namespace NetProxy
{
    internal class RequestRouter  
    {
        private readonly RoutesRepository _routes;

        public RequestRouter(RoutesRepository routes)
        {
            _routes = routes;
        }

        public RouteMapping Route(DeviceIdRequestPartial requestPartial)
        {
            if (!requestPartial.IsValid())
            {
                throw new InvalidDataException("Request is not valid");
            }

            return _routes.FirstOrDefault(requestPartial.DeviceId!.Value);
        }
    }
}