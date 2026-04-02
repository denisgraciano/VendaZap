namespace VendaZap.Domain.Enums;

public enum TenantPlan { Starter = 1, Pro = 2, Business = 3 }
public enum TenantStatus { Trial = 1, Active = 2, Suspended = 3, Cancelled = 4 }

public enum UserRole { Owner = 1, Manager = 2, Agent = 3 }

public enum ProductStatus { Active = 1, Inactive = 2, OutOfStock = 3 }

public enum ConversationStatus { Open = 1, Closed = 2, Archived = 3, WaitingHuman = 4 }
public enum ConversationMode { Bot = 1, Human = 2 }
public enum ConversationStage
{
    Initial = 1,
    Greeting = 2,
    BrowsingProducts = 3,
    ProductSelected = 4,
    CollectingQuantity = 5,
    CollectingAddress = 6,
    ConfirmingOrder = 7,
    AwaitingPayment = 8,
    OrderCompleted = 9,
    AbandonedCart = 10,
    PostSale = 11
}

public enum MessageDirection { Inbound = 1, Outbound = 2 }
public enum MessageType { Text = 1, Image = 2, Audio = 3, Video = 4, Document = 5, Template = 6, Interactive = 7 }
public enum MessageStatus { Pending = 1, Sent = 2, Delivered = 3, Read = 4, Failed = 5, Received = 6 }
public enum MessageSource { Bot = 1, Human = 2, Customer = 3, System = 4, Campaign = 5 }

public enum OrderStatus { Pending = 1, Confirmed = 2, Paid = 3, Shipped = 4, Delivered = 5, Cancelled = 6, Refunded = 7 }
public enum PaymentMethod { Pix = 1, PaymentLink = 2, Manual = 3, CreditCard = 4 }

public enum CampaignType { Broadcast = 1, AbandonedCart = 2, FollowUp = 3, Reactivation = 4, Promotional = 5 }
public enum CampaignStatus { Draft = 1, Active = 2, Paused = 3, Completed = 4, Cancelled = 5 }
public enum CampaignTrigger
{
    Manual = 1,
    AbandonedCart = 2,
    AfterFirstMessage = 3,
    AfterPurchase = 4,
    ScheduledTime = 5,
    InactiveContact = 6
}
