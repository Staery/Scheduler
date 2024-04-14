using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Scheduler.Components
{
    public class CustomDateFormatter : IDateFormatter
    {
        public string GenerateCustomDate()
        {
            CultureInfo customCulture = new CultureInfo("en-US");
            DateTime currentDate = DateTime.Today;

            // Getting the month name
            string monthName = currentDate.ToString("MMMM", customCulture);

            // Getting the day of the month with the correct suffix
            string dayOfMonthWithSuffix = $"{currentDate.Day}{GetDaySuffix(currentDate.Day)}";

            // Getting the abbreviated day of the week name
            string abbreviatedDayOfWeek = currentDate.ToString("ddd", customCulture);

            // Forming the result string using string interpolation
            return $"{monthName} {dayOfMonthWithSuffix} {abbreviatedDayOfWeek}";

            // Method for obtaining the correct suffix for the day number

            // Метод для получения правильного суффикса для числа дня
            string GetDaySuffix(int day)
            {
                if (day >= 11 && day <= 13)
                {
                    return "th";
                }

                switch (day % 10)
                {
                    case 1: return "st";
                    case 2: return "nd";
                    case 3: return "rd";
                    default: return "th";
                }
            }

        }
    }
}
