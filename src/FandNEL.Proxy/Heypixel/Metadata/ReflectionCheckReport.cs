namespace FandNEL.Proxy.Heypixel;

public class ReflectionCheckReport
{
    public string? FinalAction { get; set; }

    public string? AccessPath { get; set; }

    public List<string> ChainSteps { get; set; } = [];

    public int ChainDepth { get; set; }

    public bool HasSecurityRisk { get; set; }

    public string? SecurityWarning { get; set; }
}
