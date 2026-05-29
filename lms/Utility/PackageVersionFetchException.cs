namespace lms.Utility
{
    public class PackageVersionsFetchException : Exception
    {
        public string PackageName { get; }
        public string Server { get; }
        public System.Net.HttpStatusCode? StatusCode { get; }

        public PackageVersionsFetchException(
            string packageName,
            string server,
            string message,
            Exception? innerException = null,
            System.Net.HttpStatusCode? statusCode = null
        )
            : base(message, innerException)
        {
            PackageName = packageName;
            Server = server;
            StatusCode = statusCode;
        }
    }
}
