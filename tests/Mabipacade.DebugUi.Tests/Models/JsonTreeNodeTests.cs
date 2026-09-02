using Mabipacade.DebugUi.Models;

namespace Mabipacade.DebugUi.Tests.Models;

public class JsonTreeNodeTests
{
    [Fact]
    public void Leaf_ShowsNameAndValue()
    {
        var root = JsonTreeNode.FromJson("""{"name":"蘑菇嫩煎雞","level":200}""", "decoded");
        root.IsExpanded = true;

        Assert.Equal("name: \"蘑菇嫩煎雞\"", root.Children[0].Summary);
        Assert.Equal("level: 200", root.Children[1].Summary);
        Assert.False(root.Children[0].HasChildren);
    }

    [Fact]
    public void Object_SummarisesItsFieldCount()
    {
        var root = JsonTreeNode.FromJson("""{"a":{"x":1,"y":2,"z":3}}""", "decoded");
        root.IsExpanded = true;

        Assert.Equal("a  {3}", root.Children[0].Summary);
        Assert.True(root.Children[0].HasChildren);
    }

    [Fact]
    public void Array_SummarisesItsLength_AndIndexesChildren()
    {
        var root = JsonTreeNode.FromJson("""{"items":[{"id":1},{"id":2}]}""", "decoded");
        root.IsExpanded = true;
        var items = root.Children[0];

        Assert.Equal("items  [2]", items.Summary);
        items.IsExpanded = true;
        Assert.Equal("[0]  {1}", items.Children[0].Summary);
        Assert.Equal("[1]  {1}", items.Children[1].Summary);
    }

    [Fact]
    public void Children_AreBuiltOnlyWhenExpanded()
    {
        // A 0x5209 holds 804 items and 116 quests, each several levels deep.
        // Building the whole tree up front would freeze the UI on selection, so
        // a collapsed node must not have materialised its real children yet.
        var root = JsonTreeNode.FromJson("""{"items":[{"id":1},{"id":2}]}""", "decoded");
        root.IsExpanded = true;
        var items = root.Children[0];

        Assert.False(items.IsExpanded);
        Assert.False(items.ChildrenMaterialised);

        items.IsExpanded = true;
        Assert.True(items.ChildrenMaterialised);
        Assert.Equal(2, items.Children.Count);
    }

    [Fact]
    public void LongStrings_AreTruncatedInTheSummary()
    {
        // Quest descriptions run to hundreds of characters; the full value stays
        // available, but a tree row has to stay one line.
        var longText = new string('字', 400);
        var root = JsonTreeNode.FromJson($$"""{"description":"{{longText}}"}""", "decoded");
        root.IsExpanded = true;

        var leaf = root.Children[0];
        Assert.True(leaf.Summary.Length < 200);
        Assert.EndsWith("…", leaf.Summary);
        Assert.Equal(longText, leaf.Value);
    }

    [Fact]
    public void Root_ReportsTheTopLevelShape()
    {
        var root = JsonTreeNode.FromJson("""{"a":1,"b":2}""", "decoded");
        Assert.Equal("decoded  {2}", root.Summary);
        Assert.True(root.HasChildren);
    }

    [Fact]
    public void NullAndBool_RenderReadably()
    {
        var root = JsonTreeNode.FromJson("""{"error":null,"complete":true}""", "decoded");
        root.IsExpanded = true;

        Assert.Equal("error: null", root.Children[0].Summary);
        Assert.Equal("complete: true", root.Children[1].Summary);
    }
}
