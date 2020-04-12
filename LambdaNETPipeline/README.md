# CDK Project to build Serverless .NET Lambda Pipelines

This is a CDK project that sets up CodePipelines for a .NET Core 3.1 Lambda GitHub repository.
Two pipelines will be created one for the `master` branch and a second for a `dev` branch. The project assumes
the target repository has a `buildspec.yml` in the root of the repository.

To run this CDK project execute the following command.

`cdk deploy -c github-owner=<owner> -c github-repo=<repo-name> -c github-oauth-token=<oauth-token>`

If the GitHub oauth token is not set then the project will look for it in SSM Parameter Store using the variable name `github-oauth-token`.

## Example buildspec.yml

This project makes a couple assumptions for the buildspec.yml.
* There will be an **S3_BUCKET** environment variable that should be set. This S3 bucket is where the `dotnet package-ci` 
command will upload the build Lambda project deployment bundles.
* The output of the build will be a CloudFormation template called `updated.template` that the pipeline will pass to the 
CloudFormation stage. 

```yaml
version: 0.2

env:
    variables:
        S3_BUCKET: ""
phases:
  install:
    runtime-versions:
        dotnet: 3.1    
    commands:
      - dotnet tool install -g Amazon.Lambda.Tools
  build:
    commands:
      - cd ./ServerlessAwsSdkChangeLogAPI
      - /root/.dotnet/tools/dotnet-lambda package-ci --config-file codebuild-defaults.json --serverless-template serverless.template --output-template updated.template --s3-bucket $S3_BUCKET --s3-prefix ServerlessAwsSdkChangeLogAPIPackageCIArtifacts/
      - cd ..
artifacts:
  files:
    - ./ServerlessAwsSdkChangeLogAPI/updated.template
  discard-paths: yes
```