using System;
using System.Collections.Generic;

using Amazon.CDK;

using Amazon.CDK.AWS.CloudFormation;
using Amazon.CDK.AWS.CodeBuild;
using Amazon.CDK.AWS.CodePipeline;
using Amazon.CDK.AWS.CodePipeline.Actions;
using Amazon.CDK.AWS.IAM;
using Amazon.CDK.AWS.S3;

using Amazon.SimpleSystemsManagement;
using Amazon.SimpleSystemsManagement.Model;

namespace LambdaNetPipeline
{
    public class LambdaNetPipelineStack : Stack
    {
        Artifact_ SourceOutput { get; } = new Artifact_("Source");

        internal LambdaNetPipelineStack(Construct scope, string id, IStackProps props = null) : base(scope, id, props)
        {            
            var codeBuildRole = new Role(this, "CodeBuildRole", new RoleProps
            {
                ManagedPolicies = new IManagedPolicy[] { ManagedPolicy.FromAwsManagedPolicyName("PowerUserAccess") },
                AssumedBy = new ServicePrincipal("codebuild.amazonaws.com")
            });

            var cloudFormationRole = new Role(this, "CloudFormationRole", new RoleProps
            {
                ManagedPolicies = new IManagedPolicy[] { ManagedPolicy.FromAwsManagedPolicyName("AdministratorAccess") },
                AssumedBy = new ServicePrincipal("cloudformation.amazonaws.com")
            });

            var artifactStore = new Bucket(this, "ArtifactStore");


            var build = new PipelineProject(this, id, new PipelineProjectProps
            {
                Role = codeBuildRole,
                Environment = new BuildEnvironment
                {
                    BuildImage = LinuxBuildImage.AMAZON_LINUX_2_3,
                    EnvironmentVariables = new Dictionary<string, IBuildEnvironmentVariable>
                    {
                        {"S3_BUCKET", new BuildEnvironmentVariable{Type = BuildEnvironmentVariableType.PLAINTEXT, Value = artifactStore.BucketName } }
                    }
                }
            });


            var githubOwner = this.Node.TryGetContext("github-owner")?.ToString();
            if(string.IsNullOrEmpty(githubOwner))
            {
                throw new Exception("Context variable \"github-owner\" required to be set.");
            }

            var githubRepository = this.Node.TryGetContext("github-repo")?.ToString();
            if (string.IsNullOrEmpty(githubRepository))
            {
                throw new Exception("Context variable \"github-repo\" required to be set.");
            }

            var githubOauthToken = this.Node.TryGetContext("github-oauth-token")?.ToString();
            if (string.IsNullOrEmpty(githubOauthToken))
            {
                Console.WriteLine($"Looking for GitHub oauth token in SSM Parameter Store using key: {DEFAULT_OAUTH_PARAMETER_STORE_KEY}");
                githubOauthToken = FetchGitHubPersonalAuthToken();
            }

            Console.WriteLine($"Defining pipelines for {githubOwner}/{githubRepository}");


            CreatePipeline(cloudFormationRole, build, githubOwner, githubRepository, githubOauthToken, "dev");
            CreatePipeline(cloudFormationRole, build, githubOwner, githubRepository, githubOauthToken, "master");
        }

        private void CreatePipeline(IRole cloudFormationRole, PipelineProject build, string githubOwner, string githubRepository, string githubOauthToken, string gitBranch)
        {
            Console.WriteLine($"... defining {gitBranch} pipeline");

            var buildOutput = new Artifact_("BuildOutput");
            

            new Pipeline(this, "Pipeline-" + gitBranch, new PipelineProps
            {
                Stages = new StageProps[]
                {
                    new StageProps
                    {
                        StageName = "Source",
                        Actions = new IAction[]
                        {
                            new GitHubSourceAction(new GitHubSourceActionProps
                            {
                                ActionName = "GitHubSource",
                                Branch = gitBranch,
                                Repo = githubRepository,
                                Owner = githubOwner,
                                OauthToken = SecretValue.PlainText(githubOauthToken),
                                Output = SourceOutput
                            })
                        }
                    },
                    new StageProps
                    {
                        StageName = "Build",
                        Actions = new IAction[]
                        {
                            new CodeBuildAction(new CodeBuildActionProps
                            {
                                ActionName = $"Build-{gitBranch}",
                                Project = build,
                                Input = SourceOutput,
                                Outputs = new Artifact_[] { buildOutput }
                            })
                        }
                    },
                    new StageProps
                    {
                        StageName = "Deploy",
                        Actions = new IAction[]
                        {
                            new CloudFormationCreateUpdateStackAction(new CloudFormationCreateUpdateStackActionProps
                            {
                                ActionName = "DeployServerlessTemplate",
                                Capabilities = new CloudFormationCapabilities[] { CloudFormationCapabilities.ANONYMOUS_IAM, CloudFormationCapabilities.AUTO_EXPAND },
                                TemplatePath = ArtifactPath_.ArtifactPath(buildOutput.ArtifactName, "updated.template"),
                                StackName = $"{githubRepository}-{gitBranch}-{DateTime.Now.Ticks}",
                                AdminPermissions = true
                            })
                        }
                    }
                }
            });
        }

        const string DEFAULT_OAUTH_PARAMETER_STORE_KEY = "github-oauth-token";
        private string FetchGitHubPersonalAuthToken()
        {
            var client = new AmazonSimpleSystemsManagementClient();
            var response = client.GetParameterAsync(new GetParameterRequest
            {
                Name = DEFAULT_OAUTH_PARAMETER_STORE_KEY,
                WithDecryption = true
            }).GetAwaiter().GetResult();

            return response.Parameter.Value;
        }
    }
}
