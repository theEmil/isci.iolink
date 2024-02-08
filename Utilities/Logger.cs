using System;

namespace openDCOSIoLink.Utilities
{
    public class Logger
    {
        public int logLevel {get; set;}
        

        /// <summary>
        /// Logs a message with a specified level of importance.
        /// </summary>
        /// <param name="message">The message to be logged.</param>
        /// <param name="Importance">The importance level of the message. Defaults to 0 if not provided.</param>
        /// <remarks>
        /// The message will only be logged if its importance level is less than or equal to the current log level.
        /// Loglevels:
        /// 0 = Debug,
        /// 1 = Info,
        /// 2 = Warning,
        /// 3 = Error
        /// </remarks>
        public void log(string message, int Importance = 0)
        {
            if (Importance >= logLevel)
            {
                Console.WriteLine(message);
            }
        }
        public Logger(int logLevel = 1)
        {
            this.logLevel = logLevel;
        }
    }
}