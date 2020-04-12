using Amazon.CDK;
using System;
using System.Collections.Generic;
using System.Linq;

namespace LambdaNetPipeline
{
    sealed class Program
    {
        public static void Main(string[] args)
        {
            var app = new App();
            var stackName = (app.Node.TryGetContext("stack-name") ?? app.Node.TryGetContext("github-repo"))?.ToString();
            Console.WriteLine($"Stack name set to: {stackName}");

            new LambdaNetPipelineStack(app, stackName);
            app.Synth();
        }
    }
}
