using Pulumi;

namespace ToBeDone.Helpers;

public class ResourceNameHelper
{
    public static string AppendEnvWithDash(string name) =>
        $"{name}-{Deployment.Instance.StackName.ToLower().Substring(0, 3)}";
    
    public static string AppendEnv(string name) =>
        $"{name}{Deployment.Instance.StackName.ToLower().Substring(0, 3)}";
}