import { wordFor } from '../../lib/words';

/**
 * Turning what the log stores into what a supervisor reads.
 *
 * The log stores what the system knows: `Modified` of a `BagPalletAssignment`, or a
 * refused `Pallets.ScanBag`. Those are the right things to store — they are exact, and
 * they never need rewriting when the wording changes — but nobody reads a shift's
 * history in that language.
 *
 * Anything not listed falls through to its raw name rather than being hidden or shown as
 * "undefined". A new kind of record must appear in the log the day it exists, even if
 * nobody has written a sentence for it yet.
 */

/** What kind of thing it happened to. */
const things: Record<string, string> = {
  Material: 'a material',
  MaterialCategory: 'a material category',
  MaterialPackaging: 'a material packaging',
  Product: 'a product',
  ProductType: 'a product type',
  Mould: 'a mould',
  Color: 'a colour',
  Unit: 'a unit',
  ProductionLine: 'a production line',
  Shift: 'a shift time',
  MovementType: 'a movement type',
  RecipeFamily: 'a recipe',
  RecipeVersion: 'a recipe version',
  RecipeIngredient: 'a recipe ingredient',
  ApplicationUser: 'a worker',
  ApplicationRole: 'a role',
  ShiftReport: 'a shift',
  BagPalletAssignment: 'a bag on a pallet',
  WoodenPallet: 'a pallet',
  MaterialIssueTicket: 'an issue ticket',
};

/** What was done to it. */
const deeds: Record<string, string> = {
  Added: 'Created',
  Modified: 'Changed',
  Deleted: 'Removed',
};

/**
 * A refused attempt, named by the screen it came from rather than by a table — because
 * a refusal never touched a table.
 */
const refusals: Record<string, string> = {
  'Pallets.ScanBag': 'Scanning a bag onto a pallet',
  'Pallets.StartPallet': 'Starting a pallet',
  'Pallets.CancelPallet': 'Cancelling a pallet',
  'Pallets.ReverseAssignment': 'Taking a bag off a pallet',
  'Production.CreateRoll': 'Logging a roll',
  'Production.SaveTestReport': 'Recording a roll test',
  'Thermo.StartRun': 'Putting a roll into the thermo',
  'Thermo.FinishRun': 'Taking a roll out of the thermo',
  'Thermo.SaveTestReport': 'Counting a run',
  'Packaging.Save': 'Recording packaging',
  'Recycler.Save': 'Recording what the recycler made',
  'MaterialIssue.Create': 'Issuing material',
  'MaterialIssue.RecordReturns': 'Weighing material back in',
  'MaterialIssue.Close': 'Closing an issue ticket',
  'ShiftReports.Open': 'Opening a shift',
  'ShiftReports.Close': 'Closing a shift',
  'ShiftReports.Reopen': 'Reopening a shift',
  'Users.Create': 'Adding a worker',
  'Users.Update': 'Changing a worker',
  'Users.ResetPassword': 'Setting a password',
  'Inventory.Receive': 'Receiving material',
  'Inventory.Adjust': 'Correcting a stock figure',
  'Auth.Login': 'Signing in',
  // A screen renewing a sign-in that has run out. It appears in the log without
  // anybody pressing anything, which is why it says so plainly.
  'Auth.Refresh': 'Renewing a sign-in',
};

/**
 * One line of the log as a sentence.
 *
 * A refusal reads as the thing that was attempted; a success reads as what was done and
 * to what.
 */
export function describe(
  action: string,
  objectType: string,
  objectId: number | null,
  rejected: boolean,
): string {
  if (rejected) {
    return wordFor(refusals, action);
  }

  const deed = wordFor(deeds, action);
  const thing = wordFor(things, objectType);

  return objectId === null
    ? `${deed} ${thing}`
    : `${deed} ${thing} (#${String(objectId)})`;
}

/**
 * The kinds of thing worth filtering by, in the order somebody would look for them.
 *
 * Each carries <b>every name the log uses for it</b>. A successful reversal is recorded
 * against the entity it changed; a refused scan never touched an entity and is recorded
 * against the shape the screen asked for. Filtering on one name alone would show the
 * corrections and quietly hide the refusals — the half a supervisor came for.
 *
 * So every group lists the table names and the DTO names side by side. The rule for
 * adding one: a refusal is logged against the type the endpoint returns, which for a
 * write is its DTO and for a delete is the row's DTO (see `ApiControllerBase`).
 *
 * Nothing is ever lost by leaving a name out — "Anything" is the default and shows the
 * lot. What is lost is the ability to *find* it, which is the whole point of the screen.
 */
export const filterableThings: { value: string; label: string }[] = [
  {
    value: 'WoodenPallet,BagPalletAssignment,PalletDto',
    label: 'Pallets and bags on them',
  },
  { value: 'RollDto', label: 'Rolls' },
  { value: 'ThermoRunDto', label: 'Thermoforming runs' },
  { value: 'PackagingConsumptionDto', label: 'Packaging' },
  { value: 'RecyclerProductionDto', label: 'The recycler' },
  { value: 'ShiftReport,ShiftReportDto', label: 'Shifts' },
  { value: 'MaterialIssueTicket,IssueTicketDto', label: 'Issue tickets' },
  { value: 'MaterialStockDto', label: 'Stock figures' },
  {
    value: 'RecipeFamily,RecipeVersion,RecipeIngredient,RecipeFamilyDto,RecipeVersionDto',
    label: 'Recipes',
  },
  { value: 'ApplicationUser,ApplicationRole,UserDto', label: 'Workers and roles' },
  // Sign-ins that were refused: a wrong password, a locked account, a session that ran
  // out. The one kind of line that is about somebody who could not get in at all.
  { value: 'AuthenticationResult', label: 'Signing in' },
  {
    value:
      'Material,MaterialCategory,MaterialPackaging,MaterialDto,MaterialCategoryDto,MaterialPackagingDto',
    label: 'Materials',
  },
  // Moulds and product types share `LookupDto` on the server — both are a name and a
  // flag — so they cannot be told apart by type, and they sit in one group.
  {
    value: 'Product,ProductType,Mould,ProductDto,LookupDto',
    label: 'Products and moulds',
  },
  {
    value:
      'Color,Unit,ProductionLine,Shift,MovementType,ColorDto,UnitDto,ProductionLineDto,ShiftDto',
    label: 'Other master data',
  },
];
