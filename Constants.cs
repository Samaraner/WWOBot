using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Zählbot
{
    class Constants
    {
        //links
        public const string consthreadlink = "http://werwolf-online.com/index.php/Thread/";
        public const string consaddpostlink = "http://werwolf-online.com/index.php/PostAdd/";
        public const string consuserlink = "http://werwolf-online.com/index.php/User/";
        public const string consuserlistlink = "http://werwolf-online.com/index.php/MembersList/";
        public const string consgamesubforum = "http://werwolf-online.com/index.php/Board/10";
        public const string conspagediv = "/?pageNo=";

        //classes
        public const string consclassmessage = "wbbPost";
        public const string consclassmessageheader = "messageHeader";
        public const string consclassdatetime = "datetime";
        public const string consclassarticle = "article";
        public const string consclassmessagetext = "messageText";
        public const string consclassusercount = "boxHeadline";
        public const string consclassuserbox = "box48";

        //attributes
        public const string consattrthreadid = "data-thread-id";
        public const string consattruserid = "data-user-id";
        public const string consattrpostid = "data-post-id";
        public const string consattrtitle = "title";
        public const string consattrobjectid = "data-object-id";

        //regex
        public const string consregexvotehd = @"alt="":!:""\s*>\s*";
        public const string consregexvotelynch = @"alt="":arrow:""\s*>\s*";
        public const string consregexlynchtime = @"Lynchung:(.*\d{1,2}\.\d{1,2}.*?)?\s*(?<hour>\d{1,2})(:(?<min>\d{0,2})|\s*Uhr)";

        //errors
        public const string conserrorlynchtime = "Lynchzeit konnte nicht ausgelesen werden.";
        public const string conserrorvote = "Der Vote konnte nicht erkannt werden.";
        public const string conserrorHD = "Konnte den (neuen) HD nicht auslesen";
        public const string conserrorplayerlist = "Spielerliste nicht gefunden";

        //other
        public const int PostsperPage = 20;
    }
}
