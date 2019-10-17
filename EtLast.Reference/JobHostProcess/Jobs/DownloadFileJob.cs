﻿namespace FizzCode.EtLast
{
    using System;
    using System.Diagnostics;
    using System.Globalization;
    using System.Net;
    using System.Threading;

    public class DownloadFileJob : AbstractJob
    {
        public string Url { get; set; }
        public string FileName { get; set; }

        public override void Execute(CancellationTokenSource cancellationTokenSource)
        {
            if (string.IsNullOrEmpty(Url))
                throw new JobParameterNullException(Process, this, nameof(Url));
            if (string.IsNullOrEmpty(FileName))
                throw new JobParameterNullException(Process, this, nameof(FileName));

            Process.Context.Log(LogSeverity.Information, Process, "({Job}) downloading file from '{Url}' to '{FileName}'",
                Name, Url, PathHelpers.GetFriendlyPathName(FileName));

            // todo: use HttpClient instead with cancellationTokenSource
            var startedOn = Stopwatch.StartNew();
            using (var clt = new WebClient())
            {
                try
                {
                    clt.DownloadFile(Url, FileName);
                    Process.Context.Log(LogSeverity.Debug, Process, "({Job}) successfully downloaded from '{Url}' to '{FileName}' in {Elapsed}",
                        Name, Url, PathHelpers.GetFriendlyPathName(FileName), startedOn.Elapsed);
                }
                catch (Exception ex)
                {
                    var exception = new JobExecutionException(Process, this, "file download failed", ex);
                    exception.AddOpsMessage(string.Format(CultureInfo.InvariantCulture, "file download failed, url: {0}, file name: {1}, message: {2}",
                        Url, FileName, ex.Message));
                    exception.Data.Add("Url", Url);
                    exception.Data.Add("FileName", FileName);
                    throw exception;
                }
            }
        }
    }
}