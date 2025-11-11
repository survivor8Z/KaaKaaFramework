using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Tag集合
/// </summary>
[Serializable]
public class TagCollection
{
    [SerializeField] private List<Tag> tags = new List<Tag>();

    public bool Contains(Tag tag)
    {
        if (tag == null) return false;
        return tags.Contains(tag);
    }

    public bool Contains(string key)
    {
        return tags.Exists(t => t.Key == key);
    }

    public void Add(Tag tag)
    {
        if (tag != null && !Contains(tag))
        {
            tags.Add(tag);
        }
    }

    public void Remove(Tag tag)
    {
        tags.Remove(tag);
    }

    public bool Check(List<Tag> requireTags, List<Tag> excludeTags = null)
    {
        // 检查是否需要所有requireTags
        if (requireTags != null && requireTags.Count > 0)
        {
            foreach (var requireTag in requireTags)
            {
                if (requireTag != null && !Contains(requireTag))
                {
                    return false;
                }
            }
        }

        // 检查是否包含excludeTags中的任何一个
        if (excludeTags != null && excludeTags.Count > 0)
        {
            foreach (var excludeTag in excludeTags)
            {
                if (excludeTag != null && Contains(excludeTag))
                {
                    return false;
                }
            }
        }

        return true;
    }

    public IReadOnlyList<Tag> Tags => tags;
}

