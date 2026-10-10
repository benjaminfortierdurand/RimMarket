# RimMarket

Your colony becomes a market town, RimWorld 1.6. You build the stalls, merchants show up and set up shop in them, and you tax the lot.

## What it does

- A market marker with an adjustable square (radius, or auto-fit to your walls) and empty stalls you build around it.
- Trade caravans that find a free stall walk over and set up: the merchant stands behind the counter with the wares laid out and one guard stands post beside the stall. The pack animals wait at the caravan camp, a spot on the ground you can wall in. The other guards sit around the guard rest spot, chat and eat from their packs. Without them, the caravan waits just outside the market, out of the shoppers' way.
- Trade from the stall or from the merchant with the vanilla trade window. The merchant stays the trader, nothing replaces the economy.
- A stall fee on arrival, paid in silver on the counter.
- One haggle attempt per visit, straight from the trade window.
- Idle colonists browse the stalls for recreation. They never spend colony silver on their own.
- With Hospitality, guests buy from the merchants, and the market can take a share of every sale instead of a fee on arrival.
- Shoppers from friendly factions walk over while a merchant is set up, spend their own silver and go home a few hours later.
- Trade routes: talk to a faction by comms console (or by bird post with Medieval Overhaul), pay a fee in silver, and one of their towns sends a merchant convoy to your market every week or two. You can watch it crawl across the world map. Good days strengthen the route; bad ones wear it out.
- Any stall can be made a colony stall: a sales table says what to sell, how many and at what price, your haulers bring the goods over, and shoppers and guests pay at the counter. An optional Shopkeep work type puts a colonist behind it. With Vanilla Trading Expanded, what the stall sells counts toward its supply and demand.
- Any bed can be set to Market inn, like a medical bed. The sheets change colour, colonists keep out, and merchants, off-duty guards and tired shoppers rent it by the night. A nice private room is good for the market's name. A bedroll in a shed at a stiff price is not.
- A full market day, then they pack up and leave by the usual road. A button on the marker sends everyone home early.

## Requirements

- RimWorld 1.6
- [Harmony](https://steamcommunity.com/sharedfiles/filedetails/?id=2009463077)

## Plays nicely with

Hospitality, Trading Control, Trading Spot, RimToll, Rim War, Vanilla Trading Expanded. No hard dependency on any of them.

## Credits

The market stall art is by Tomson Johnson.

The auction post texture is by Gerrymon, from Misc Props: Dungeon, used with permission.

## Building

```
dotnet build Source\RimMarket.csproj -c Release --nologo -v q
```

The DLL lands in `1.6/Assemblies/`. `publish.ps1` mirrors the mod into the RimWorld Mods folder.
