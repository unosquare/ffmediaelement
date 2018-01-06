using System.Windows.Threading;
using Unosquare.FFME.Shared;

namespace Unosquare.FFME.Platform
{
    internal class WindowsPlatformFactory : IPlatformFactory
    {
        public IDispatcherTimer CreateDispatcherTimer(ActionPriority priority)
        {
            return new WindowsDispatcherTimer((DispatcherPriority)priority);
        }

        public IMediaEventConnector CreateEventConnector()
        {
            return new WindowsEventConnector(this);
        }

        public INativeMethodsProvider CreateNativeMethodsProvider()
        {
            throw new System.NotImplementedException();
        }

        public IMediaRenderer CreateRenderer(MediaType mediaType, MediaEngine mediaEngine)
        {
            throw new System.NotImplementedException();
        }
    }
}
