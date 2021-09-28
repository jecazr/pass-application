using Commons.Enums;
using System;
using System.Collections.Generic;
using System.Fabric;
using System.Linq;
using System.Threading.Tasks;

namespace Commons.Utilities
{
    public class Utils
    {
        private static string applicationName = null;

        public static string GetApplicationName(ServiceContext serviceContext)
        {
            if (applicationName == null)
            {
                applicationName = serviceContext.CodePackageActivationContext.ApplicationName;
            }
            return applicationName;
        }

        public static Uri GetCitizenDataServiceName(ServiceContext serviceContext)
        {
            return new Uri($"{GetApplicationName(serviceContext)}/CitizenData");
        }

        public static Uri GetCitizenDataProxyAddress(ServiceContext serviceContext)
        {
            return GetProxyAddress(GetCitizenDataServiceName(serviceContext));
        }

        public static Uri GetApplicationDataServiceName(ServiceContext serviceContext)
        {
            return new Uri($"{GetApplicationName(serviceContext)}/ApplicationData");
        }

        public static Uri GetApplicationDataProxyAddress(ServiceContext serviceContext)
        {
            return GetProxyAddress(GetApplicationDataServiceName(serviceContext));
        }

        public static Uri GetApplicationWebServiceName(ServiceContext serviceContext)
        {
            return new Uri($"{GetApplicationName(serviceContext)}/ApplicationWeb");
        }

        public static Uri GetApplicationWebProxyAddress(ServiceContext serviceContext)
        {
            return GetProxyAddress(GetApplicationWebServiceName(serviceContext));
        }

        public static Uri GetAdminAPIServiceName(ServiceContext serviceContext)
        {
            return new Uri($"{GetApplicationName(serviceContext)}/AdminAPI");
        }

        public static Uri GetAdminAPIProxyAddress(ServiceContext serviceContext)
        {
            return GetProxyAddress(GetAdminAPIServiceName(serviceContext));
        }

        public static Uri GetApplicationApproverServiceName(ServiceContext serviceContext)
        {
            return new Uri($"{GetApplicationName(serviceContext)}/ApplicationApprover");
        }

        public static Uri GetApplicationApproverProxyAddress(ServiceContext serviceContext)
        {
            return GetProxyAddress(GetApplicationApproverServiceName(serviceContext));
        }

        public static Uri GetProxyAddress(Uri serviceName)
        {
            return new Uri($"http://localhost:19081{serviceName.AbsolutePath}");
        }

        public static long GetPartitionKey(string citizenId)
        {
            long idNumber = long.Parse(citizenId);
            return idNumber % 2;
        }

        
    }
}
