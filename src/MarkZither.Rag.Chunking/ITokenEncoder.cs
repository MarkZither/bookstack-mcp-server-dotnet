namespace MarkZither.Rag.Chunking;

public interface ITokenEncoder
{
    int CountTokens(string text);
}
