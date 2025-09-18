namespace Sandstorm.Orchestrator;

record class OrchestratorEndpoint(string Endpoint)
{
    public override string ToString() => Endpoint;
    public static implicit operator string(OrchestratorEndpoint endpoint) => endpoint.Endpoint;
}