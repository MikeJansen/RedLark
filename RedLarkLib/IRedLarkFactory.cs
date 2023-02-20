namespace RedLarkLib;

public interface IRedLarkFactory
{
    IRedLark New(IEnumerable<string> a_hosts, int? a_retryCount = null, int? a_retryDelayMin = null, int? a_retryDelayMax = null, string? a_name = null);
}
