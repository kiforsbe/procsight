namespace ProcSight.Application.Settings;
public interface ICredentialStore
{
    string? Get(string providerId);
    void Set(string providerId,string value);
    void Remove(string providerId);
}
