/**
 * The stretch of days a range report covers.
 *
 * Held by the Reports screen rather than by each report, so switching between them keeps
 * the days the reader chose — asking for the same fortnight three times is the sort of
 * thing that makes a screen tiring.
 */
export interface DateRange {
  from: string;
  to: string;
}

/**
 * A date this many days from now, as the yyyy-mm-dd a date input wants.
 *
 * The default range ends <b>tomorrow</b> rather than today: a night shift starting this
 * evening carries tomorrow's production date, so a range ending today would hide the
 * shift that is running while the report is read.
 *
 * Re-exported rather than written here. It used to be written here, from `toISOString`,
 * which reports UTC — so between midnight and three in the morning it gave the day
 * before, and the range ended *today* instead of tomorrow. It hid the running night
 * shift, which is the one thing this default exists to show.
 */
export { dayFromNow } from '../../lib/dates';
