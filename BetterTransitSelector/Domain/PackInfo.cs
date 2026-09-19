namespace BetterTransitSelector.Domain {
    #region Using Statements

    using System.Collections.Generic;

    using Colossal.UI.Binding;

    #endregion

    /// <summary>
    /// What the game's own mod browser knows about an upload, for the picker's pack page.
    /// </summary>
    /// <remarks>
    /// Every field is read from <c>pdx_mods_cache.json</c>, the cache the game keeps for its mod
    /// browser -- no request of ours goes anywhere. Image fields are the PDX CDN URLs the cache
    /// holds; whether the in-game view renders them is the UI's problem, and it falls back when
    /// not. The cache only holds subscribed uploads, so a vanilla vehicle has no page.
    /// </remarks>
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
        /// <summary>Likes as a fraction of ratings on PDX -- in practice always 5; the count is the figure worth showing.</summary>
        public float  Rating;
        /// <summary>The number of likes: a PDX rating is a thumbs-up, not a star.</summary>
        public int    RatingsTotal;
        public int    Subscriptions;
        /// <summary>The upload's size in bytes.</summary>
        public long   Size;
        public string Cover = string.Empty;
        public string ForumLink = string.Empty;
        public readonly List<string> Screenshots = new List<string>();
        public readonly List<(string type, string url)> Links = new List<(string, string)>();
        public readonly List<(string id, string name, bool installed)> Dependencies = new List<(string, string, bool)>();

        /// <inheritdoc/>
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
