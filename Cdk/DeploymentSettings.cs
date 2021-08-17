using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Cdk
{
    public class DeploymentSettings
    {
        public bool AttachDebugger { get; set; }
        public string AwsAccountId { get; set; }
        public string AwsRegion { get; set; }

        public int WebHostInstanceCount { get; set; }
        public double WebHostCpuCount { get; set; }
        public double WebHostMemoryLimit { get; set; }
    }
}
