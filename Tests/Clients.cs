﻿using System;
using System.Linq;
using AE.Net.Mail;
using AE.Net.Mail.Imap;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Should.Fluent;

namespace Tests {
  [TestClass]
  public class UnitTest1 {
    public TestContext TestContext { get; set; }

    [TestMethod]
    public void TestIDLE() {
      var mre = new System.Threading.ManualResetEvent(false);
      using (var imap = GetClient<ImapClient>()) {
        imap.NewMessage += (sender, e) => {
          var msg = imap.GetMessage(e.MessageCount - 1);
          Console.WriteLine(msg.Subject);
        };

        while (!mre.WaitOne(5000)) //low for the sake of testing; typical timeout is 30 minutes
          imap.Noop();
      }
    }

    void imap_NewMessage(object sender, MessageEventArgs e) {
      var imap = (sender as ImapClient);
      var msg = imap.GetMessage(e.MessageCount - 1);
      Console.WriteLine(msg.Subject);
    }

    [TestMethod]
    public void TestConnections() {
      var accountsToTest = System.IO.Path.Combine(Environment.CurrentDirectory.Split(new[] { "\\AE.Net.Mail\\" }, StringSplitOptions.RemoveEmptyEntries).First(), "ae.net.mail.usernames.txt");
      var lines = System.IO.File.ReadAllLines(accountsToTest)
          .Select(x => x.Split(','))
          .Where(x => x.Length == 6)
          .ToArray();

      lines.Any(x => x[0] == "imap").Should().Be.True();
      lines.Any(x => x[0] == "pop3").Should().Be.True();

      foreach (var line in lines)
        using (var mail = GetClient(line[0], line[1], int.Parse(line[2]), bool.Parse(line[3]), line[4], line[5])) {
          mail.GetMessageCount().Should().Be.InRange(1, int.MaxValue);

          var msg = mail.GetMessage(0, true);
          msg.Subject.Should().Not.Be.NullOrEmpty();
          msg = mail.GetMessage(0, false);
          msg.Body.Should().Not.Be.NullOrEmpty();
        }
    }

    [TestMethod]
    public void TestSearchConditions() {
      var deleted = SearchCondition.Deleted();
      var seen = SearchCondition.Seen();
      var text = SearchCondition.Text("andy");

      deleted.ToString().Should().Equal("DELETED");
      deleted.Or(seen).ToString().Should().Equal("OR (DELETED) (SEEN)");
      seen.And(text).ToString().Should().Equal("(SEEN) (TEXT \"andy\")");

      var since = new DateTime(2000, 1, 1);
      SearchCondition.Undeleted().And(
                  SearchCondition.From("david"),
                  SearchCondition.SentSince(since)
              ).Or(SearchCondition.To("andy"))
          .ToString()
          .Should().Equal("OR ((UNDELETED) (FROM \"david\") (SENTSINCE \"" + Utilities.GetRFC2060Date(since) + "\")) (TO \"andy\")");
    }

    [TestMethod]
    public void TestSearch() {
      using (var imap = GetClient<ImapClient>()) {
        var result = imap.SearchMessages(
          //"OR ((UNDELETED) (FROM \"david\") (SENTSINCE \"01-Jan-2000 00:00:00\")) (TO \"andy\")"
            SearchCondition.Undeleted().And(SearchCondition.From("david"), SearchCondition.SentSince(new DateTime(2000, 1, 1))).Or(SearchCondition.To("andy"))
            );
        result.Length.Should().Be.InRange(1, int.MaxValue);
        result.First().Value.Subject.Should().Not.Be.NullOrEmpty();

        result = imap.SearchMessages(new SearchCondition { Field = SearchCondition.Fields.Text, Value = "asdflkjhdlki2uhiluha829hgas" });
        result.Length.Should().Equal(0);
      }
    }

    private T GetClient<T>(string host = "gmail", string type = "imap") where T : class, IMailClient {
      var accountsToTest = System.IO.Path.Combine(Environment.CurrentDirectory.Split(new[] { "\\AE.Net.Mail\\" }, StringSplitOptions.RemoveEmptyEntries).First(), "ae.net.mail.usernames.txt");
      var lines = System.IO.File.ReadAllLines(accountsToTest)
          .Select(x => x.Split(','))
          .Where(x => x.Length == 6)
          .ToArray();

      var line = lines.Where(x => x[0].Equals(type) && (x.ElementAtOrDefault(1) ?? string.Empty).Contains(host)).FirstOrDefault();
      return GetClient(line[0], line[1], int.Parse(line[2]), bool.Parse(line[3]), line[4], line[5]) as T;
    }

    private IMailClient GetClient(string type, string host, int port, bool ssl, string username, string password) {
      if ("imap".Equals(type, StringComparison.OrdinalIgnoreCase)) {
        return new AE.Net.Mail.ImapClient(host, username, password, AE.Net.Mail.ImapClient.AuthMethods.Login, port, ssl);
      }

      if ("pop3".Equals(type, StringComparison.OrdinalIgnoreCase)) {
        return new AE.Net.Mail.Pop3Client(host, username, password, port, ssl);
      }

      throw new NotImplementedException(type + " is not implemented");
    }
  }
}
