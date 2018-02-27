using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HtmlAgilityPack;
using static Zählbot.Data;

namespace Zählbot
{
    class Program
    {
        static void Main(string[] args)
        {
            Bot bot = new Bot();
            bot.Start();

        }

    }
}
