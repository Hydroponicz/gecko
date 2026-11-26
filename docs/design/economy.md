# Economy, Early Game & Selling — Design

Status: **draft**

## Money
| | Dirty money | Clean money |
|---|---|---|
| Comes from | Crime: robberies, selling product, heists | Laundering, legit business income |
| Spent on | Supplies, worker/driver/guard wages, black-market goods | Properties, legit purchases |

**General rule:** almost anything can be bought with dirty money, but it costs **far more** than in
clean money. Laundering is how you stop overpaying. The game is a criminal economic sandbox:
many ways to make money, each with its own margins and risks.

## Cash on you vs physical cash
- **Wallet:** dirty cash on your person is just a number, capped at **$5,000**.
- **Big amounts are physical:**
  - At a property, dirty cash sits on the **cash table** next to the Stash as visible **$1,000 stacks**.
  - It moves around in **duffel bags** (up to **$10,000** each). You carry up to **3 bags** on your back.
  - Carrying 3 bags weighs you down: no sprinting, no jumping, and (once weapons exist) no shooting.
  - Bags can go in vehicles (vehicles step).
- If you receive more than your wallet holds (robbery, sale), the rest goes into duffel bags you carry.
- Cash table options: deposit from wallet, take to wallet, pack duffel bag, unpack bags
  (tap = $1,000 or one bag, hold = all).
- Later: workers' wages come from the cash table; laundering businesses take bags and pay into the bank.
- **Clean money** is a bank balance (a number), unused until laundering exists.
- Balances show top-right for a few seconds when they change, or press **Z / D-pad down**.

## Buying supplies
- Call the supplier from a **pay phone** in the street or a **landline** you place in build mode.
- Orders: 5 Paper ($100), 5 Ink ($100), or both ($200), paid from your wallet (dirty prices, placeholders).
- Delivered to the property's Stash after ~30 seconds; the phone shows a countdown.
- Pay phone orders go to your (only) property; with several properties you'll pick one.

## Early game: starting from nothing
1. The player robs **convenience stores, gas stations and fast-food places** for their first dirty money.
2. They meet a **shady contact** who sells them their first property for dirty money, at a price
   well above its clean-money value (paying a premium for unlaundered cash).
   Alternatively/also: they **buy small amounts of drugs from a supplier and resell them** on the street.
   The margin is thin compared to producing it themselves, which is the motivation to start a business.
3. They set up production (see production.md).

## Selling: transport is required
- There is **no instant sale**. Finished product must be physically transported to **buyers and distributors**.
- Early: the player loads product from the Stash into a vehicle and drives it to the buyer.
- Later: the player hires **drivers** and **guards** to run deliveries automatically.
- Delivery pays dirty money and generates heat (police/heat system TBD).
- **Buyers:** take small amounts at a high price per unit.
- **Distributors:** take bulk at a lower price per unit.
- **Automated deliveries can fail** (police stop, rival hijack, etc.). Chance depends on the driver's
  and guards' skills and the vehicle used.

## Open questions
- Exact dirty-money markup (e.g. 2x?) and whether it varies per seller.
