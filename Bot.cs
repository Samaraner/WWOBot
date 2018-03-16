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
using SHDocVw;
using mshtml;

namespace Zählbot
{
    class Bot
    {
        HtmlWeb web = new HtmlWeb();
        Dictionary<int, string> playerNames = new Dictionary<int, string>();
        //TODO: threadid hinzufügen
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
            gameList.Where(x => x.Active).ToList().ForEach(x => RunGame(ref x));
            //gameList.Where(x => x.Active && ScanThread(ref x)).ToList().ForEach(x => ConstructPost(x));
            Thread.Sleep(10000);
            Run(gameList);

            void RunGame(ref Game game)
            {
                var req = ScanThread(ref game);
                var post = ConstructPost(game, req);
                SendPost(post, game.ThreadId.ToString());
            }
            errorList = new List<Error>();
        }

        //TODO: Requests verwenden
        private string ConstructPost(Game game, List<Request> req)
        {
            string ret = string.Empty;
            var sys = req.Where(x => x.System).LastOrDefault();
            if (sys != null && sys.Day == game.CurrentDay().Day)
            {
                ret += ConstructStand(game);
                ret += Environment.NewLine + Environment.NewLine + ConstructHistorie(game);
                ret += Environment.NewLine + Environment.NewLine + ConstructErrors(game.ThreadId);
            }
            return ret;
        }

        private void SendPost(string input, string id)
        {
            InternetExplorer explorer = new InternetExplorer();
            explorer.Visible = true;
            explorer.Navigate($@"{consaddpostlink}{id}");
            while (explorer.Busy)
            {
                Thread.Sleep(100);
            }

            HTMLDocument doc = (HTMLDocument)explorer.Document;

            IHTMLElement field = doc.getElementById("text").parentElement.children.item(1, null);
            field.innerText = input;

            IHTMLElement button = doc.getElementById("previewButton").parentElement.children.item(0, null);
            button.click();

            explorer.Quit();

        }

        private string ConstructStand(Game game)
        {
            string ret = string.Empty;

            if (game.WithHD && game.CurrentDay().Day == 1)
            {
                ret = $@"[b]Stand HD-Wahl:[/b]{Environment.NewLine}";
            }
            else
            {
                ret = $@"[b]Stand Lynchung {game.CurrentDay().Day}:[/b]{Environment.NewLine}";
            }
            GetOnlyCurrentVotes(game.CurrentDay().Votes).Select(x => x.Voted).Distinct().ToList().ForEach(x => ret += SingleVoted(x));

            return ret;


            string SingleVoted(Player voted)
            {
                string retlocal = $@"[b]{voted.Name}[/b]:";
                int votes = 0;
                List<string> votees = new List<string>();

                var votelist = game.CurrentDay().Votes.Where(x => x.Voted.PlayerId == voted.PlayerId).Distinct();
                foreach (var vote in votelist)
                {
                    if (game.WithHD && game.CurrentDay().Day > 1 && vote.Voting.PlayerId == game.HD.PlayerId)
                    {
                        votees.Add($@"[b]{vote.Voting.Name}[/b]");
                        votes += game.HDStimmen; 
                    }
                    else
                    {
                        votees.Add(vote.Voting.Name);
                        votes++;
                    }
                }
                return $@"[b]{voted.Name}[/b]: {votes.ToString()} ({String.Join(", ",votees)}){Environment.NewLine}";
            }
        }

        private string ConstructHistorie(Game game)
        {
            string ret = "[b]Verlauf:[/b]";
            game.CurrentDay().Votes.ForEach(x => ret += Environment.NewLine + GetSingleHistoryElement(x));
            return ret;

            string GetSingleHistoryElement(Vote input)
            {
                return $@"[url='{consthreadlink}{game.ThreadId}/postID={input.Postid}#post{input.Postid}']{input.Voting.Name} stimmt auf {input.Voted.Name}[/url]";
            }
        }

        //TODO
        private string ConstructErrors(int gameid)
        {

            return string.Empty;
        }

        List<Vote> GetOnlyCurrentVotes(List<Vote> votes)
        {
            return votes.Select(x => votes.Where(y => y.Voting == x.Voting).Last()).Distinct().ToList();
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

        //TODO: Unterschiedliche Zeiten für HD Wahlen erkennen
        //      +Unterfunktionen
        public Game SetupGame(int id)
        {
            HtmlDocument source = web.Load(String.Concat(consthreadlink, id.ToString()));
            ConstructPost post = GetPostDataFromNode(GetNodesByClass(source.DocumentNode, consclassmessage).First());

            Game ret = new Game() { ThreadId = id, SL = post.Author};
            GameDay gameday = new GameDay() {Day = 1};
            gameday.LastPostNumber = post.PostNumber;
            gameday.Start = post.Time;
            gameday.End = GetLynchTime(post.Postcontent, post.Author, post.PostId, post.Time, id).AddDays(1);

            ret.Days.Add(gameday);
            ret.Active = true;

            ret.PlayerList = GetPlayerList(post.Postcontent, post.Author, post.PostNumber, id);
            ret.HoursPerDay = GetHoursPerDay(post.Postcontent);
            ret.WithHD = GetWithHD(post.Postcontent);

            return ret;
        }

        public List<Request> ScanThread(ref Game game)
        {
            var ret = new List<Request>();
            var lastpost = game.LastPostNumber();
            HtmlDocument source = web.Load(String.Concat(consthreadlink, game.ThreadId.ToString(), conspagediv, game.ActivePage().ToString()));
            List<ConstructPost> postList = GetNodesByClass(source.DocumentNode, consclassmessage).Where(x => x.Name == consclassarticle).Select(x => GetPostDataFromNode(x)).Where(x => x.PostNumber > lastpost).ToList();

            if (postList.Count() != 0)
            {
                foreach (var post in postList)
                {
                    if (post.Author.PlayerId == game.SL.PlayerId)
                    {
                        AnalyzeSLPost(post, ref game);
                    }
                    else
                    {
                        if (post.Time < game.CurrentDay().End && TryGetVote(post, game.PlayerList, game.GetVoteToken(), game.ThreadId, out Player voted))
                        {
                            post.Voted = voted;
                            game.AddVote(post);
                            ret.Add(new Request(game.CurrentDay().Day, true));
                        }
                    }
                    game.CurrentDay().LastPostNumber = post.PostNumber;
                }
                ret.AddRange(ScanThread(ref game));
            }
            return ret;
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

        //TODO: Mehr Schlüsselwörter erkennen
        //TODO: Spieler ersetzen erkennen(ReplacePlayer)
        private void AnalyzeSLPost(ConstructPost post, ref Game game)
        {
            if (post.Postcontent.DocumentNode.InnerText.Contains("Nachtpost") && post.Time > game.CurrentDay().End)
            {
                var current = game.CurrentDay();
                GameDay gameday = new GameDay() { Day = current.Day + 1, End = current.End.AddHours(game.HoursPerDay), Start = post.Time };
                game.Days.Add(gameday);
            }

            var list = String.Join("|", game.PlayerList.Where(x => x.Alive).Select(x => ReplaceSymbols(x.Name)));
            Regex reg = new Regex($@"(?<player>{list})?\s*(wird|ist)(\s*neuer)?\s*(Hauptmann|HD)", RegexOptions.IgnoreCase);
            var match = reg.Match(post.Postcontent.DocumentNode.InnerText);

            if (match.Success)
            {
                if (match.Groups["player"].Success)
                {
                    game.HD = game.PlayerList.Where(x => x.Name.Equals(match.Groups["player"].Value, StringComparison.InvariantCultureIgnoreCase)).First();
                }
                else
                {
                    errorList.Add(new Error(conserrorHD, post.Author, post.PostId, game.ThreadId));
                }
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

        //TODO: Mehr Chars auskommentieren
        private string ReplaceSymbols(string input)
        {
            string ret = input.Replace(" ", @"\s").Replace(".",@"\.");
            return ret;
        }

        private bool TryGetVote(ConstructPost post, List<Player> playerlist, string votetoken, int gameID, out Player voted)
        {
            voted = null;
            
            var list = String.Join("|", playerlist.Where(x => x.Alive).Select(x => x.Name));
            Regex reg = new Regex(String.Concat(votetoken, "(?<player>", list, ")?"), RegexOptions.RightToLeft | RegexOptions.IgnoreCase);
            var match = reg.Match(post.Postcontent.DocumentNode.InnerHtml);
            if (match.Success)
            {
                if (match.Groups["player"].Success)
                {
                    voted = playerlist.Where(x => x.Name.Equals(match.Groups["player"].Value, StringComparison.InvariantCultureIgnoreCase)).First();
                    return true;
                }
                else
                {
                    errorList.Add(new Error(conserrorvote, post.Author, post.PostId, gameID));
                }
            }
            return false;
        }

        private DateTime GetLynchTime(HtmlDocument content, Player author, int postid, DateTime date, int gameID)
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
                errorList.Add(new Error(conserrorlynchtime, author, postid, gameID));
            }

            return ret;
        }

        //TODO Spielererkennung verbessern
        private List<Player> GetPlayerList(HtmlDocument content, Player author, int postnumber, int gameID)
        {
            List<Player> ret = new List<Player>();

            Regex reg = new Regex($@"Spielerliste:(.*?Warteliste|.*)", RegexOptions.IgnoreCase | RegexOptions.Singleline );
            var match = reg.Match(content.DocumentNode.InnerHtml);
            if (match.Success)
            {
                foreach (var item in playerNames)
                {
                    if (match.Value.Contains(item.Value))
                    {
                        ret.Add(new Player() { PlayerId = item.Key, Alive = true, Name = item.Value });
                    }
                }
            }
            else
            {
                errorList.Add(new Error(conserrorplayerlist, author, postnumber, gameID));
            }
            return ret;
        }

        //TODO 48 Stunden Spiele erkennen
        private int GetHoursPerDay(HtmlDocument content)
        {
            return 24;
        }

        //TODO HD Wahl erkennen
        private bool GetWithHD(HtmlDocument content)
        {
            Regex reg = new Regex(@"(Kein|Ohne) (HD|Hauptmann)");
            if (reg.IsMatch(content.DocumentNode.InnerText))
            {
                return false;
            }
            return true;
        }

        //TODO
        private void ReplacePlayer(ref Game game, Player replaced, Player replacing)
        {
            game.ReplacePlayer(replaced, replacing);
            errorList = errorList.Where(x => x.Author.PlayerId != replaced.PlayerId).ToList();
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
            return node.FirstChild.InnerText.Trim();
        }

        private void CacheAllUsers()
        {
            HtmlDocument source = web.Load(consuserlistlink);
            var nodecount = GetNodesByClass(source.DocumentNode, consclassusercount).First();
            int count = Int32.Parse(nodecount.ChildNodes[1].ChildNodes[1].InnerText);
            for (int i = 1; i < ((count - 1) / 30 +1); i++)
            {
                ScanMemberListPage(i);
            }
        }

        private void ScanMemberListPage(int page)
        {
            HtmlDocument source = web.Load($"{consuserlistlink}{conspagediv}{page}");
            var nodes = GetNodesByClass(source.DocumentNode,consclassuserbox);
            foreach (var node in nodes)
            {
                string name = GetAttributeFromNode(node.ChildNodes[1],consattrtitle);
                int id = GetAttributeIntFromNode(node.ParentNode, consattrobjectid);
                playerNames.Add(id, name);
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
