using System;
using System.IO;
using Nito.Disposables;

namespace test.bctklib3
{
    static class Utility
    {
        public static string GetTempPath()
        {
            string tempPath;
            do
            {
                tempPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            }
            while (Directory.Exists(tempPath));
            return tempPath;
        }

        public static IDisposable GetDeleteDirectoryDisposable(string path)
        {
            return AnonymousDisposable.Create(() =>
            {
                if (Directory.Exists(path))
                {
                    Directory.Delete(path, true);
                }
            });
        }
    }
}
