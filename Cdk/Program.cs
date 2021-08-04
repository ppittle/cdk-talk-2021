using Amazon.CDK;

namespace Cdk
{
    sealed class Program
    {
        public static void Main(string[] args)
        {
            var app = new App();
            new AppStack(app, "Cdk-Talk-AppStack");
            app.Synth();
        }
    }
}
