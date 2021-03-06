﻿using System.Xml.Serialization;
using VpNet.Interfaces;

namespace VpNet.Abstract
{
    public class ChatMessage : IChatMessage
    {
        [XmlAttribute]
        virtual public ChatMessageTypes Type { get; set; }
        virtual public Color Color { get; set; }
        [XmlAttribute]
        virtual public TextEffectTypes TextEffectTypes { get; set; }
        [XmlAttribute]
        virtual public string Message { get; set; }
        [XmlAttribute]
        public virtual string Name { get; set; }

        public ChatMessage(string message)
        {
            Message = message;
        }

        public ChatMessage() { }
    }
}
