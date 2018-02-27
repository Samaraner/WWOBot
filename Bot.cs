using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Zählbot.Data;
using static Zählbot.Constants;
using System.Text.RegularExpressions;
using System.Threading;

namespace Zählbot
{
    class Bot
    {
        HtmlWeb web = new HtmlWeb();
        Dictionary<int, string> playerNames = new Dictionary<int, string>();
        List<Error> errorList = new List<Error>();

        public void Start()
        {
            CacheAllUsers();
            List<Game> gameList = new List<Game>();
            ScanForGames().ForEach(x => gameList.Add(SetupGame(x)));
            Run(gameList);
        }

        public void Run(List<Game> gameList)
        {
            gameList.Where(x => x.Active && ScanThread(ref x)).ToList().ForEach(x => PostStand(x));
            Thread.Sleep(10000);
            Run(gameList);
        }

        //TODO
        private void PostStand(Game game)
        {


        }

        public List<int> ScanForGames()
        {
            List<int> ret = new List<int>();
            HtmlDocument source = web.Load(consgamesubforum);
            var nodes = source.DocumentNode.SelectNodes("//h3/a");

            foreach (var node in nodes)
            {
                ret.Add(GetAttributeIntFromNode(node, consattrthreadid));
            }
            return ret;
        }

        public Game SetupGame(int id)
        {
            HtmlDocument source = web.Load(String.Concat(consthreadlink, id.ToString()));
            ConstructPost post = GetPostDataFromNode(GetNodesByClass(source.DocumentNode, consclassmessage).First());

            Game ret = new Game() { ThreadId = id, SL = post.Author};
            GameDay gameday = new GameDay() {Day = 1};
            //gameday.Posts.Add(post);
            gameday.LastPostNumber = post.PostNumber;
            gameday.Start = post.Time;
            gameday.End = GetLynchTime(post.Postcontent, post.Author, post.PostId, post.Time).AddDays(1);

            ret.Days.Add(gameday);
            ret.Active = true;

            ret.PlayerList = GetPlayerList(post.Postcontent);
            ret.BiDaily = GetBiDaily(post.Postcontent);
            ret.WithHD = GetWithHD(post.Postcontent);

            return ret;
        }

        //TODO
        public bool ScanThread(ref Game game)
        {
            var lastpost = game.LastPostNumber();
            HtmlDocument source = web.Load(String.Concat(consthreadlink, game.ThreadId.ToString(), conspagediv, game.ActivePage().ToString()));
            List<ConstructPost> postList = GetNodesByClass(source.DocumentNode, consclassmessage).Where(x => x.Name == consclassarticle).Select(x => GetPostDataFromNode(x)).Where(x => x.PostNumber > lastpost).ToList();

            if (postList.Count() == 0)
            {
                return false;
            }
            else
            {
                foreach (var post in postList)
                {
                    if (post.Author.PlayerId == game.SL.PlayerId)
                    {
                        AnalyzeSLPost(post, ref game);
                    }
                    else
                    {
                        if (post.Time < game.CurrentDay().End && GetVote(post, game.PlayerList, GetVoteToken(game), out Player voted))
                        {
                            post.Voted = voted;
                            game.AddVote(post);
                        }
                    }
                    game.CurrentDay().LastPostNumber = post.PostNumber;
                    //post.Postcontent = null;
                    //game.CurrentDay().Posts.Add(post);
                }
                return (ScanThread(ref game) | true);
            }
        }

        private ConstructPost GetPostDataFromNode(HtmlNode node)
        {
            ConstructPost ret = new ConstructPost();
            HtmlDocument doc = new HtmlDocument();
            doc.LoadHtml(node.InnerHtml);
            CleanFromQuotes(ref doc);

            ret.Author = GetAuthorFromDoc(doc);
            ret.PostNumber = GetPostNumberFromDoc(doc);
            ret.PostId = GetAttributeIntFromNode(node, consattrpostid);
            ret.Time = GetDateTimeFromDoc(doc);
            ret.Postcontent = doc;

            return ret;
        }

        private void AnalyzeSLPost(ConstructPost post, ref Game game)
        {
            if (post.Postcontent.DocumentNode.InnerText.Contains("Nachtpost"))
            {
                var current = game.CurrentDay();
                GameDay gameday = new GameDay() { Day = current.Day + 1, End = current.End.AddDays(1), Start = post.Time };
                game.Days.Add(gameday);
            }
        }

        private void CleanFromQuotes(ref HtmlDocument doc)
        {
            var nodes = doc.DocumentNode.SelectNodes("//blockquote");
            if (nodes != null)
            {
                foreach (var node in nodes)
                {
                    node.RemoveAll();
                }
            }
        }

        private bool GetVote(ConstructPost post, List<Player> playerlist, string votetoken, out Player voted)
        {
            voted = null;
            
            var list = String.Join("|", playerlist.Where(x => x.Alive).Select(x => x.Name.Replace(" ", @"\s")));
            Regex reg = new Regex(String.Concat(votetoken, "(?<player>", list , ")?"), RegexOptions.RightToLeft);
            var match = reg.Match(post.Postcontent.DocumentNode.InnerHtml);
            if (match.Success)
            {
                if (match.Groups["player"].Success)
                {
                    voted = playerlist.Where(x => x.Name == match.Groups["player"].Value).First();
                    return true;
                }
                else
                {
                    errorList.Add(new Error(conserrorvote, post.Author, post.PostId));
                }
            }
            return false;
        }

        private string GetVoteToken(Game game)
        {
            if (game.WithHD && game.CurrentDay().Day == 1)
            {
                return consregexvotehd;
            }
            else
            {
                return consregexvotelynch;
            }
        }

        private DateTime GetLynchTime(HtmlDocument content, Player author, int postid, DateTime date)
        {
            DateTime ret = new DateTime();
            Regex reg = new Regex(consregexlynchtime);
            var match = reg.Match(content.DocumentNode.InnerText);
            if (match.Success)
            {
                int min = 0;
                int hour = Int32.Parse(match.Groups["hour"].Value);
                if (match.Groups["min"].Success)
                {
                    min = Int32.Parse(match.Groups["min"].Value);
                }

                ret = new DateTime(date.Year, date.Month, date.Day, hour, min, 0);
            }
            else
            {
                errorList.Add(new Error(conserrorlynchtime, author, postid));
            }

            return ret;
        }

        //TODO
        private List<Player> GetPlayerList(HtmlDocument content)
        {
            List<Player> ret = new List<Player>();
            playerNames.ToList().ForEach(x => ret.Add(new Player() { PlayerId = x.Key, Alive = true, Name = x.Value }));

            return ret;
        }

        //TODO
        private bool GetBiDaily(HtmlDocument content)
        {
            return false;
        }

        //TODO
        private bool GetWithHD(HtmlDocument content)
        {
            return true;
        }

        private string GetUserNameByID(int id)
        {
            var ret = playerNames.Where(x => x.Key == id).FirstOrDefault().Value;
            if (ret == null)
            {
                ret = GetNewUserName(id);
                playerNames.Add(id, ret);
            }
            return ret;
        }

        private string GetNewUserName(int id)
        {
            HtmlDocument source = web.Load(String.Concat(consuserlink, id.ToString()));
            var node = source.DocumentNode.SelectSingleNode("//h1");
            if (node == null)
            {
                return string.Empty;
            }
            return RemoveWhitespace(node.FirstChild.InnerText);
        }

        private void CacheAllUsers()
        {
            int nomatch = 0;
            int id = 1;

            while (nomatch < 10)
            {
                string name = GetNewUserName(id);
                if (name != string.Empty)
                {
                    playerNames.Add(id, name);
                    nomatch = 0;
                }
                else
                {
                    nomatch++;
                }
                id++;
            }
        }

        private Player GetAuthorFromDoc(HtmlDocument doc)
        {
            Player ret = new Player();
            var node = doc.DocumentNode.SelectSingleNode("//h2/a");
            ret.PlayerId = GetAttributeIntFromNode(node, consattruserid);
            ret.Name = GetUserNameByID(ret.PlayerId);
            return ret;
        }

        private DateTime GetDateTimeFromDoc(HtmlDocument doc)
        {
            var node = GetNodesByClass(doc.DocumentNode, consclassmessageheader).First().SelectSingleNode("//p/a/time");
            string datetime = GetAttributeFromNode(node, consclassdatetime);
            var ret = DateTime.Parse(datetime);
            return ret;
        }


        private int GetPostNumberFromDoc(HtmlDocument doc)
        {
            var node = GetNodesByClass(doc.DocumentNode, consclassmessageheader).First().SelectSingleNode("//li/a");
            return Int32.Parse(RemoveWhitespace(node.FirstChild.InnerText));
        }

        private HtmlNodeCollection GetNodesByClass(HtmlNode node, string className)
        {
            return node.SelectNodes(string.Format("//*[contains(@class,'{0}')]", className));
        }

        private int GetAttributeIntFromNode(HtmlNode node, string attribut)
        {
            return Int32.Parse(GetAttributeFromNode(node,attribut));
        }

        private string GetAttributeFromNode(HtmlNode node, string attribut)
        {
            return node.Attributes[attribut].Value;
        }

        private string RemoveWhitespace(string input)
        {
            return new string(input.ToCharArray()
                .Where(c => !Char.IsWhiteSpace(c))
                .ToArray());
        }
    }
}
