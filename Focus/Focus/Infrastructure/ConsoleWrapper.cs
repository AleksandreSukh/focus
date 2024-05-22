using System;

namespace Systems.Sanity.Focus.Infrastructure
{
    public class ConsoleWrapper
    {
        public static int WindowWidth => Console.WindowWidth - 2;
        public static readonly ConsoleColor DefaultColor = ConsoleColor.Gray;
    }
}
