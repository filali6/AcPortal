namespace Backend.Modules.Contracts.Services;

public interface IPdfTextExtractor
{
    string Extract(string filePath);
}