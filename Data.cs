using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace Zählbot
{
    public class Data
    {
        internal class Player
        {
            internal int PlayerId { get; set; }
            internal string Name { get; set; }
            internal bool Alive { get; set; }
        }

        internal class Post
        {
            internal int PostId { get; set; }
            internal int PostNumber { get; set; }
            internal Player Author { get; set; }
            internal Player Voted { get; set; }
            internal DateTime Time { get; set; }
        }

        internal class ConstructPost : Post
        {
            internal HtmlAgilityPack.HtmlDocument Postcontent { get; set; }
        }

        internal class GameDay
        {
            internal int Day { get; set; }
            internal DateTime Start { get; set; }
            internal DateTime End { get; set; }
            //internal List<Post> Posts { get; set; } = new List<Post>();
            internal int LastPostNumber { get; set; }
            internal List<Vote> Votes { get; set; } = new List<Vote>();
        }

        internal class Vote
        {
            internal Player Voting { get; set; }
            internal Player Voted { get; set; }
            internal int Postid { get; set; }
            internal DateTime Time { get; set; }

            internal Vote(Player voting, Player voted, int postid, DateTime time)
            {
                this.Voting = voting;
                this.Voted = voted;
                this.Postid = postid;
                this.Time = time;
            }
        }

        internal class Game
        {
            //internal int GameNumber { get; set; }
            internal int ThreadId { get; set; }
            internal bool WithHD { get; set; }
            internal int HoursPerDay { get; set; } = 24;
            internal bool Active { get; set; }
            internal List<Player> PlayerList { get; set; }
            internal Player HD { get; set; }
            internal int HDStimmen { get; set; } = 2;
            internal Player SL { get; set; }
            internal List<GameDay> Days { get; set; } = new List<GameDay>();

            public int ActivePage(int postsperpage = Constants.PostsperPage)
            {
                return (LastPostNumber() / postsperpage) + 1;
            }

            public int LastPostNumber()
            {
                return CurrentDay().LastPostNumber;
                //return CurrentDay().Posts.Select(x => x.PostNumber).Max();
            }

            public GameDay CurrentDay()
            {
                return Days.Where(x => x.Day == Days.Select(y => y.Day).Max()).First();
            }

            internal void AddVote(Post post)
            {
                this.CurrentDay().Votes.Add(new Vote(post.Author, post.Voted, post.PostId, post.Time));
            }
        }

        internal class Error
        {
            string Message { get; set; }
            Player Author { get; set; }
            int Postid { get; set; }

            internal Error(string message, Player author, int postid)
            {
                this.Message = message;
                this.Author = author;
                this.Postid = postid;
            }
        }
    }
}
