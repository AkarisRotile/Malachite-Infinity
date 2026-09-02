using System;
using System.Collections.Generic;

namespace 可成长的孔雀翎
{
    public class DialogGroup
    {
        public string Id;
        public string DisplayNameZh;
        public string DisplayNameEn;
        public string Category;
        public int SortOrder;
        public string UnlockHintZh;
        public string UnlockHintEn;

        public List<(int face, string text)> MessagesZh = new List<(int, string)>();
        public List<(int face, string text)> MessagesEn = new List<(int, string)>();

        public DialogGroup(
            string id,
            string displayNameZh,
            string displayNameEn,
            string category,
            int sortOrder,
            string unlockHintZh,
            string unlockHintEn,
            IEnumerable<(int, string)> messagesZh,
            IEnumerable<(int, string)> messagesEn)
        {
            Id = id;
            DisplayNameZh = displayNameZh;
            DisplayNameEn = displayNameEn;
            Category = category;
            SortOrder = sortOrder;
            UnlockHintZh = unlockHintZh;
            UnlockHintEn = unlockHintEn;

            if (messagesZh != null) MessagesZh.AddRange(messagesZh);
            if (messagesEn != null) MessagesEn.AddRange(messagesEn);
        }

        public string GetDisplayName()
        {
            return MalachiteData.IsEnglish ? DisplayNameEn : DisplayNameZh;
        }

        public List<(int face, string text)> GetMessages()
        {
            return MalachiteData.IsEnglish ? MessagesEn : MessagesZh;
        }

        public string GetUnlockHint()
        {
            return MalachiteData.IsEnglish ? UnlockHintEn : UnlockHintZh;
        }
    }
}