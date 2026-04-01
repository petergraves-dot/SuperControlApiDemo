using System.Xml.Serialization;

namespace petergraves.Integrations.SuperControl;

[XmlRoot("scAPI")]
public sealed class DataExportBookingsResponse
{
    [XmlElement("CurrentPage")]
    public int CurrentPage { get; set; }

    [XmlElement("TotalPages")]
    public int TotalPages { get; set; }

    [XmlArray("Payload")]
    [XmlArrayItem("Booking")]
    public List<DataExportBooking> Bookings { get; set; } = [];
}

public sealed class DataExportBooking
{
    [XmlElement("SystemId")]
    public string SystemId { get; set; } = string.Empty;

    [XmlElement("BookingId")]
    public int BookingId { get; set; }

    [XmlElement("BookingDate")]
    public string BookingDate { get; set; } = string.Empty;

    [XmlElement("Type")]
    public string Type { get; set; } = string.Empty;

    [XmlElement("Status")]
    public string Status { get; set; } = string.Empty;

    [XmlElement("Source")]
    public string Source { get; set; } = string.Empty;

    [XmlElement("Currency")]
    public string Currency { get; set; } = string.Empty;

    [XmlElement("ClientRef")]
    public string ClientRef { get; set; } = string.Empty;

    [XmlElement("Guest")]
    public DataExportGuest? Guest { get; set; }

    [XmlArray("Properties")]
    [XmlArrayItem("Property")]
    public List<DataExportBookingPropertyItem> Properties { get; set; } = [];
}

public sealed class DataExportGuest
{
    [XmlElement("Title")]
    public string Title { get; set; } = string.Empty;

    [XmlElement("FirstName")]
    public string FirstName { get; set; } = string.Empty;

    [XmlElement("LastName")]
    public string LastName { get; set; } = string.Empty;

    [XmlElement("Address1")]
    public string Address1 { get; set; } = string.Empty;

    [XmlElement("Address2")]
    public string Address2 { get; set; } = string.Empty;

    [XmlElement("Town")]
    public string Town { get; set; } = string.Empty;

    [XmlElement("Postcode")]
    public string Postcode { get; set; } = string.Empty;

    [XmlElement("County")]
    public string County { get; set; } = string.Empty;

    [XmlElement("Country")]
    public string Country { get; set; } = string.Empty;

    [XmlElement("TelMain")]
    public string TelMain { get; set; } = string.Empty;

    [XmlElement("TelMobile")]
    public string TelMobile { get; set; } = string.Empty;

    [XmlElement("Subscribed")]
    public string Subscribed { get; set; } = string.Empty;

    [XmlElement("Email")]
    public string Email { get; set; } = string.Empty;

    [XmlElement("GuestId")]
    public string GuestId { get; set; } = string.Empty;
}

public sealed class DataExportBookingPropertyItem
{
    [XmlElement("Start")]
    public string Start { get; set; } = string.Empty;

    [XmlElement("End")]
    public string End { get; set; } = string.Empty;

    [XmlElement("Closed")]
    public string Closed { get; set; } = string.Empty;

    [XmlElement("PropertyId")]
    public int PropertyId { get; set; }

    [XmlElement("VatRate")]
    public string VatRate { get; set; } = string.Empty;

    [XmlElement("Status")]
    public string Status { get; set; } = string.Empty;

    [XmlElement("Adults")]
    public int Adults { get; set; }

    [XmlElement("Childrens")]
    public int Childrens { get; set; }

    [XmlElement("Infants")]
    public int Infants { get; set; }

    [XmlElement("RefundableDeposit")]
    public string RefundableDeposit { get; set; } = string.Empty;

    [XmlElement("Total")]
    public decimal Total { get; set; }

    [XmlElement("AgencyFee")]
    public decimal AgencyFee { get; set; }

    [XmlElement("CanxFee")]
    public decimal CanxFee { get; set; }
}

[XmlRoot("scdata")]
public sealed class DataExportPropertiesResponse
{
    [XmlElement("property")]
    public List<DataExportPropertyItem> Properties { get; set; } = [];
}

public sealed class DataExportPropertyItem
{
    [XmlElement("supercontrolID")]
    public string SupercontrolId { get; set; } = string.Empty;

    [XmlElement("propertyname")]
    public string PropertyName { get; set; } = string.Empty;

    [XmlElement("arrive")]
    public string Arrive { get; set; } = string.Empty;

    [XmlElement("depart")]
    public string Depart { get; set; } = string.Empty;

    [XmlElement("address")]
    public string Address { get; set; } = string.Empty;

    [XmlElement("town")]
    public string Town { get; set; } = string.Empty;

    [XmlElement("postcode")]
    public string Postcode { get; set; } = string.Empty;

    [XmlElement("country")]
    public string Country { get; set; } = string.Empty;

    [XmlElement("longitude")]
    public string Longitude { get; set; } = string.Empty;

    [XmlElement("latitude")]
    public string Latitude { get; set; } = string.Empty;
}
