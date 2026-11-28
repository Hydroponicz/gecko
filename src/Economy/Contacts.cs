using System.Collections.Generic;
using Godot;

namespace Gecko.Economy;

/// <summary>
/// The starting contacts and where they like to meet (positions in the gray-box city).
/// Names are placeholders. Times are game minutes (1 game day = 48 real minutes).
/// </summary>
public static class Contacts
{
    private const double TwoDays = 2 * 24 * 60;

    public static List<Contact> CreateStarting() =>
    [
        new Contact("Tommy Two-Times", ContactKind.Buyer, pricePerBrick: 1_000, minPerDeal: 1, maxPerDeal: 5,
            limitBeforeCooldown: 8, cooldownMinutes: TwoDays,
            [
                new MeetingSpot("Behind the laundromat on East Ave", new Vector3(32f, 0f, -17f)),
                new MeetingSpot("The alley off East Ave", new Vector3(50f, 0f, 5f)),
                new MeetingSpot("South Street corner", new Vector3(-15f, 0f, 32f)),
            ]),
        new Contact("Lady Rose", ContactKind.Buyer, pricePerBrick: 1_100, minPerDeal: 1, maxPerDeal: 4,
            limitBeforeCooldown: 6, cooldownMinutes: TwoDays,
            [
                new MeetingSpot("Outside the diner on South Street", new Vector3(12f, 0f, 20.5f)),
                new MeetingSpot("The lot on West Ave", new Vector3(-29f, 0f, -60f)),
                new MeetingSpot("By the newsstand on East Ave", new Vector3(31.5f, 0f, -45f)),
            ]),
        new Contact("Pier 9 Freight", ContactKind.Distributor, pricePerBrick: 700, minPerDeal: 10, maxPerDeal: 40,
            limitBeforeCooldown: 0, cooldownMinutes: 0,
            [
                new MeetingSpot("Pier 9 loading dock", new Vector3(69f, 0f, -30.5f)),
            ]),
    ];
}
