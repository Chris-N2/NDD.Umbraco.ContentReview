// Umbraco types are imported here, never written partly qualified: inside the
// NDD.Umbraco.* namespace, "Umbraco.Cms..." resolves to NDD.Umbraco and fails to compile.
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;

namespace NDD.Umbraco.ContentReview.Composing;

/// <summary>
/// The entry point for registering the content review services. Umbraco discovers it on
/// startup. Nothing is registered yet.
/// </summary>
public sealed class ContentReviewComposer : IComposer
{
    public void Compose(IUmbracoBuilder builder)
    {
    }
}
