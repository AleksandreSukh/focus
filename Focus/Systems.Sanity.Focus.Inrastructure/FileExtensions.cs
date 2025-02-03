namespace Systems.Sanity.Focus.Infrastructure;

public static class FileExtensions
{
    public static string NameWithoutExtension(this FileInfo fileInfo)
    {
        return fileInfo.Name.Remove(fileInfo.Name.Length - fileInfo.Extension.Length);
    }
}