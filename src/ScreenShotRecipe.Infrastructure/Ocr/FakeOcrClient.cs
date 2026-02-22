using System.Threading.Tasks;
using ScreenShotRecipe.Domain.Interfaces;

namespace ScreenShotRecipe.Infrastructure.Ocr
{
    public class FakeOcrClient : IOcrClient
    {
        public Task<OcrResult> RecognizeAsync(byte[] imageBytes)
        {
            // Mock OCR text extracted from recipe images
            var mockText = @"Chocolate Chip Cookies

INGREDIENTS:
2 1/4 cups all-purpose flour
1 tsp baking soda
1 tsp salt
1 cup butter, softened
3/4 cup granulated sugar
3/4 cup packed brown sugar
2 large eggs
2 tsp vanilla extract
2 cups chocolate chips

INSTRUCTIONS:
1. Preheat oven to 375°F (190°C)
2. Mix flour, baking soda and salt in small bowl
3. Beat butter and sugars until creamy
4. Add eggs and vanilla extract
5. Gradually blend in flour mixture
6. Stir in chocolate chips
7. Drop by rounded tablespoon onto ungreased baking sheets
8. Bake for 9-11 minutes or until golden brown
9. Cool on baking sheets for 2 minutes
10. Remove to wire racks to cool completely";

            return Task.FromResult(new OcrResult(mockText, 0.95));
        }
    }
}
