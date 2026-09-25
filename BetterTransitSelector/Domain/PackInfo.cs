namespace BetterTransitSelector.Domain {
    #region Using Statements

    using System.Collections.Generic;

    using Colossal.UI.Binding;

    #endregion

    public sealed class PackInfo : IJsonWritable {
        public bool   Valid;
        public string Group = string.Empty;
        public string Id = string.Empty;
        public string Title = string.Empty;
        public string Author = string.Empty;
        public string Created = string.Empty;
        public string Updated = string.Empty;
        public string Version = string.Empty;
        public string ShortDescription = string.Empty;
        public string LongDescription = string.Empty;

        public float  Rating;

        public int    RatingsTotal;
        public int    Subscriptions;

        public long   Size;
        public string Cover = string.Empty;
        public string ForumLink = string.Empty;
        public readonly List<string> Screenshots = new List<string>();
        public readonly List<(string type, string url)> Links = new List<(string, string)>();
        public readonly List<(string id, string name, bool installed)> Dependencies = new List<(string, string, bool)>();

        public void Write(IJsonWriter writer) {
            writer.TypeBegin("BetterTransitSelector.PackInfo");
            writer.PropertyName("valid");            writer.Write(Valid);
            writer.PropertyName("group");            writer.Write(Group);
            writer.PropertyName("id");               writer.Write(Id);
            writer.PropertyName("title");            writer.Write(Title);
            writer.PropertyName("author");           writer.Write(Author);
            writer.PropertyName("created");          writer.Write(Created);
            writer.PropertyName("updated");          writer.Write(Updated);
            writer.PropertyName("version");          writer.Write(Version);
            writer.PropertyName("shortDescription"); writer.Write(ShortDescription);
            writer.PropertyName("longDescription");  writer.Write(LongDescription);
            writer.PropertyName("rating");           writer.Write(Rating);
            writer.PropertyName("ratingsTotal");     writer.Write(RatingsTotal);
            writer.PropertyName("subscriptions");    writer.Write(Subscriptions);
            writer.PropertyName("size");             writer.Write(Size);
            writer.PropertyName("cover");            writer.Write(Cover);
            writer.PropertyName("forumLink");        writer.Write(ForumLink);

            writer.PropertyName("screenshots");
            writer.ArrayBegin(Screenshots.Count);
            foreach (var s in Screenshots) writer.Write(s);
            writer.ArrayEnd();

            writer.PropertyName("links");
            writer.ArrayBegin(Links.Count);
            foreach (var (type, url) in Links) {
                writer.TypeBegin("BetterTransitSelector.PackLink");
                writer.PropertyName("type"); writer.Write(type);
                writer.PropertyName("url");  writer.Write(url);
                writer.TypeEnd();
            }
            writer.ArrayEnd();

            writer.PropertyName("dependencies");
            writer.ArrayBegin(Dependencies.Count);
            foreach (var (id, name, installed) in Dependencies) {
                writer.TypeBegin("BetterTransitSelector.PackDependency");
                writer.PropertyName("id");        writer.Write(id);
                writer.PropertyName("name");      writer.Write(name);
                writer.PropertyName("installed"); writer.Write(installed);
                writer.TypeEnd();
            }
            writer.ArrayEnd();

            writer.TypeEnd();
        }
    }
}
