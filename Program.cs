using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

class Program
{
    static async Task Main(string[] args)
    {
        // https://envrosym.azurewebsites.net/
        // var client = new HttpClient { BaseAddress = new Uri("https://localhost:7021/") };
        var client = new HttpClient { BaseAddress = new Uri("https://envrosym.azurewebsites.net/") };
        const string apiKey = "u0000770"; // Replace with your actual API key

        // Add the API key to the default request headers
        client.DefaultRequestHeaders.Add("X-Api-Key", apiKey);

        while (true)
        {
            Console.WriteLine("Simulation Control:");
            Console.WriteLine("1. Control Fan");
            Console.WriteLine("2. Control Heater");
            Console.WriteLine("3. Read Temperature");
            Console.WriteLine("4. Display State of All Devices");
            Console.WriteLine("5. Control Simulation");
            Console.WriteLine("6. Reset Simulation");
            Console.Write("Select an option: ");

            var input = Console.ReadLine();
            switch (input)
            {
                case "1":
                
                    Console.Write("Enter Fan Number: ");
                    if (int.TryParse(Console.ReadLine(), out int fanId))
                    {
                        Console.Write("Turn Fan On or Off? (on/off): ");
                        var stateInput = Console.ReadLine();
                        bool isOn = stateInput?.ToLower() == "on";

                        try
                        {
                            await SetFanState(client, fanId, isOn);
                            Console.WriteLine($"Fan {fanId} has been turned {(isOn ? "On" : "Off")}.");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error: {ex.Message}");
                        }
                    }
                    else
                    {
                        Console.WriteLine("Invalid Fan Number.");
                    }
                    break;
                case "2":
                    
                    Console.Write("Enter Heater Number: ");
                    if (int.TryParse(Console.ReadLine(), out int heaterId))
                    {
                        Console.Write("Set Heater Level (0-5): ");
                        if (int.TryParse(Console.ReadLine(), out int level) && level >= 0 && level <= 5)
                        {
                            try
                            {
                                await SetHeaterLevel(client, heaterId, level);
                                Console.WriteLine($"Heater {heaterId} level set to {level}.");
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"Error: {ex.Message}");
                            }
                        }
                        else
                        {
                            Console.WriteLine("Invalid Heater Level. Please enter a value between 0 and 5.");
                        }
                    }
                    else
                    {
                        Console.WriteLine("Invalid Heater Number.");
                    }
                    break;
                case "3":
                    
                    Console.Write("Enter Sensor Number: ");
                    if (int.TryParse(Console.ReadLine(), out int sensorId))
                    {
                        try
                        {
                            double temperature = await GetSensorTemperature(client, sensorId);
                            Console.WriteLine($"Sensor {sensorId} Temperature: {temperature:F1}°C");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error: {ex.Message}");
                        }
                    }
                    else
                    {
                        Console.WriteLine("Invalid Sensor Number.");
                    }
                    break;
                case "4":
                    Console.WriteLine("Fetching the state of all devices...");

                    try
                    {
                        Console.WriteLine("Fetching fan states individually...");
                        for (int i = 1; i <= 3; i++) // Assuming there are 3 fans for this example
                        {
                            var fanResponse = await client.GetAsync($"api/fans/{i}/state");
                            if (fanResponse.IsSuccessStatusCode)
                            {
                                var fanJson = await fanResponse.Content.ReadAsStringAsync();
                                var fan = JsonSerializer.Deserialize<FanDTO>(fanJson, new JsonSerializerOptions
                                {
                                    PropertyNameCaseInsensitive = true
                                });
                                Console.WriteLine($"  Fan {fan.Id}: {(fan.IsOn ? "On" : "Off")}");
                            }
                            else
                            {
                                Console.WriteLine($"  Fan {i}: Failed to fetch state.");
                            }
                        }
                        Console.WriteLine("Fetching heater levels individually...");
                        for (int i = 1; i <= 3; i++) // Assuming there are 3 heaters for this example
                        {
                            var heaterResponse = await client.GetAsync($"api/heat/{i}/level");
                            if (heaterResponse.IsSuccessStatusCode)
                            {
                                var levelString = await heaterResponse.Content.ReadAsStringAsync();
                                if (int.TryParse(levelString, out int level))
                                {
                                    Console.WriteLine($"  Heater {i}: Level {level}");
                                }
                                else
                                {
                                    Console.WriteLine($"  Heater {i}: Failed to parse level.");
                                }
                            }
                            else
                            {
                                Console.WriteLine($"  Heater {i}: Failed to fetch level.");
                            }
                        }
                        Console.WriteLine("Fetching sensor temperatures individually...");
                        try
                        {
                            var sensor1Temp = await GetSensor1Temperature(client);
                            Console.WriteLine($"  Sensor 1: Temperature {sensor1Temp} (Deg)");

                            var sensor2Temp = await GetSensor2Temperature(client);
                            Console.WriteLine($"  Sensor 2: Temperature {sensor2Temp} (Deg)");

                            var sensor3Temp = await GetSensor3Temperature(client);
                            Console.WriteLine($"  Sensor 3: Temperature {sensor3Temp} (Deg)");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error fetching sensor data: {ex.Message}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error fetching device states: {ex.Message}");
                    }
                    break;
                case "5":
                    //await RunTemperatureControlLoop(client);
                    Console.WriteLine("Starting temperature control algorithm...");
                    Console.Write("Provide a final Temp Value: ");
                    double currentTemperature = await GetAverageTemperature(client);
                    while (true)
                    {
                        // Phase 1: Gradually increase to 20°C over 30 seconds
                        currentTemperature = await AdjustTemperature(client, currentTemperature, 20.0, 30);

                        // Phase 2: Rapidly cool to 16°C
                        currentTemperature = await AdjustTemperature(client, currentTemperature, 16.0, 10);

                        // Phase 3: Hold at 16°C for 10 seconds
                        currentTemperature = await HoldTemperature(client, currentTemperature, 16.0, 10);

                        // Phase 4: Gradually return to 18°C and maintain
                        currentTemperature = await AdjustTemperature(client, currentTemperature, 18.0, 20);
                        currentTemperature = await HoldTemperature(client, currentTemperature, 18.0, int.MaxValue); // Maintain until exit
                    }
                case "6":
                    // await Reset(client);
                    Console.WriteLine("Resetting client state...");

                    try
                    {
                        // Send a POST request to the reset endpoint
                        var response = await client.PostAsync("api/Envo/reset", null);

                        if (response.IsSuccessStatusCode)
                        {
                            Console.WriteLine("Client state has been successfully reset.");
                            Console.WriteLine("Fetching the state of all devices...");

                            try
                            {
                                // Get individual fan states
                                Console.WriteLine("Fetching fan states individually...");
                                for (int i = 1; i <= 3; i++) // Assuming there are 3 fans for this example
                                {
                                    var fanResponse = await client.GetAsync($"api/fans/{i}/state");
                                    if (fanResponse.IsSuccessStatusCode)
                                    {
                                        var fanJson = await fanResponse.Content.ReadAsStringAsync();
                                        var fan = JsonSerializer.Deserialize<FanDTO>(fanJson, new JsonSerializerOptions
                                        {
                                            PropertyNameCaseInsensitive = true
                                        });
                                        Console.WriteLine($"  Fan {fan.Id}: {(fan.IsOn ? "On" : "Off")}");
                                    }
                                    else
                                    {
                                        Console.WriteLine($"  Fan {i}: Failed to fetch state.");
                                    }
                                }

                                // Get individual heater levels
                                Console.WriteLine("Fetching heater levels individually...");
                                for (int i = 1; i <= 3; i++) // Assuming there are 3 heaters for this example
                                {
                                    var heaterResponse = await client.GetAsync($"api/heat/{i}/level");
                                    if (heaterResponse.IsSuccessStatusCode)
                                    {
                                        var levelString = await heaterResponse.Content.ReadAsStringAsync();
                                        if (int.TryParse(levelString, out int level))
                                        {
                                            Console.WriteLine($"  Heater {i}: Level {level}");
                                        }
                                        else
                                        {
                                            Console.WriteLine($"  Heater {i}: Failed to parse level.");
                                        }
                                    }
                                    else
                                    {
                                        Console.WriteLine($"  Heater {i}: Failed to fetch level.");
                                    }
                                }

                                // Get individual sensor temperatures
                                Console.WriteLine("Fetching sensor temperatures individually...");
                                try
                                {
                                    var sensor1Temp = await GetSensor1Temperature(client);
                                    Console.WriteLine($"  Sensor 1: Temperature {sensor1Temp} (Deg)");

                                    var sensor2Temp = await GetSensor2Temperature(client);
                                    Console.WriteLine($"  Sensor 2: Temperature {sensor2Temp} (Deg)");

                                    var sensor3Temp = await GetSensor3Temperature(client);
                                    Console.WriteLine($"  Sensor 3: Temperature {sensor3Temp} (Deg)");
                                }
                                catch (Exception ex)
                                {
                                    Console.WriteLine($"Error fetching sensor data: {ex.Message}");
                                }
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"Error fetching device states: {ex.Message}");
                            }
                        }
                        else
                        {
                            Console.WriteLine($"Failed to reset client state: {response.ReasonPhrase}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error while resetting client state: {ex.Message}");
                    }
                    break;
                default:
                    Console.WriteLine("Invalid option. Please try again.");
                    break;
            }

            Console.WriteLine(); // Add a blank line for better readability
        }
    }

    private static async Task Reset(HttpClient client)
    {
        Console.WriteLine("Resetting client state...");

        try
        {
            // Send a POST request to the reset endpoint
            var response = await client.PostAsync("api/Envo/reset", null);

            if (response.IsSuccessStatusCode)
            {
                Console.WriteLine("Client state has been successfully reset.");
                Console.WriteLine("Fetching the state of all devices...");

                try
                {
                    // Get individual fan states
                    Console.WriteLine("Fetching fan states individually...");
                    for (int i = 1; i <= 3; i++) // Assuming there are 3 fans for this example
                    {
                        var fanResponse = await client.GetAsync($"api/fans/{i}/state");
                        if (fanResponse.IsSuccessStatusCode)
                        {
                            var fanJson = await fanResponse.Content.ReadAsStringAsync();
                            var fan = JsonSerializer.Deserialize<FanDTO>(fanJson, new JsonSerializerOptions
                            {
                                PropertyNameCaseInsensitive = true
                            });
                            Console.WriteLine($"  Fan {fan.Id}: {(fan.IsOn ? "On" : "Off")}");
                        }
                        else
                        {
                            Console.WriteLine($"  Fan {i}: Failed to fetch state.");
                        }
                    }

                    // Get individual heater levels
                    Console.WriteLine("Fetching heater levels individually...");
                    for (int i = 1; i <= 3; i++) // Assuming there are 3 heaters for this example
                    {
                        var heaterResponse = await client.GetAsync($"api/heat/{i}/level");
                        if (heaterResponse.IsSuccessStatusCode)
                        {
                            var levelString = await heaterResponse.Content.ReadAsStringAsync();
                            if (int.TryParse(levelString, out int level))
                            {
                                Console.WriteLine($"  Heater {i}: Level {level}");
                            }
                            else
                            {
                                Console.WriteLine($"  Heater {i}: Failed to parse level.");
                            }
                        }
                        else
                        {
                            Console.WriteLine($"  Heater {i}: Failed to fetch level.");
                        }
                    }

                    // Get individual sensor temperatures
                    Console.WriteLine("Fetching sensor temperatures individually...");
                    try
                    {
                        var sensor1Temp = await GetSensor1Temperature(client);
                        Console.WriteLine($"  Sensor 1: Temperature {sensor1Temp} (Deg)");

                        var sensor2Temp = await GetSensor2Temperature(client);
                        Console.WriteLine($"  Sensor 2: Temperature {sensor2Temp} (Deg)");

                        var sensor3Temp = await GetSensor3Temperature(client);
                        Console.WriteLine($"  Sensor 3: Temperature {sensor3Temp} (Deg)");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error fetching sensor data: {ex.Message}");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error fetching device states: {ex.Message}");
                }
            }
            else
            {
                Console.WriteLine($"Failed to reset client state: {response.ReasonPhrase}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error while resetting client state: {ex.Message}");
        }
    }

    static async Task RunTemperatureControlLoop(HttpClient client)
    {
        Console.WriteLine("Starting temperature control algorithm...");

        double currentTemperature = await GetAverageTemperature(client);

        // Prompt user for the final target temperature in Phase 4
        Console.Write("Enter the final target temperature for Phase 4: ");
        if (!double.TryParse(Console.ReadLine(), out double finalTargetTemperature))
        {
            Console.WriteLine("Invalid input. Please enter a valid numeric temperature.");
            return;
        }

        while (true)
        {
            // Phase 1: Gradually increase to 20°C over 30 seconds
            currentTemperature = await AdjustTemperature(client, currentTemperature, 20.0, 30);

            // Phase 2: Rapidly cool to 16°C
            currentTemperature = await AdjustTemperature(client, currentTemperature, 16.0, 10);

            // Phase 3: Hold at 16°C for 10 seconds
            currentTemperature = await HoldTemperature(client, currentTemperature, 16.0, 10);

            // Phase 4: Gradually adjust to the user-defined target temperature and maintain
            currentTemperature = await AdjustTemperature(client, currentTemperature, finalTargetTemperature, 20);
            currentTemperature = await HoldTemperature(client, currentTemperature, finalTargetTemperature, int.MaxValue); // Maintain until exit
        }
    }

    static async Task<double> AdjustTemperature(HttpClient client, double currentTemperature, double targetTemperature, int durationSeconds)
    {
        Console.WriteLine($"Adjusting temperature to {targetTemperature}°C over {durationSeconds} seconds...");
        int intervalMs = 1000; // 1-second intervals
        int iterations = durationSeconds;

        for (int i = 0; i < iterations; i++)
        {
            if (Math.Abs(currentTemperature - targetTemperature) <= 0.1) break;

            if (currentTemperature < targetTemperature)
            {
                // Turn on heaters and reduce fan activity
                await SetAllHeaters(client, 3); // Set heaters to level 3
                await SetAllFans(client, false); // Turn off fans
            }
            else
            {
                // Turn off heaters and increase fan activity
                await SetAllHeaters(client, 0); // Turn off heaters
                await SetAllFans(client, true); // Turn on fans
            }

            // Wait for a second and fetch the updated temperature
            await Task.Delay(intervalMs);
            currentTemperature = await GetAverageTemperature(client);
            Console.WriteLine($"Current Temperature: {currentTemperature:F1}°C");
        }

        return currentTemperature;
    }

    static async Task<double> HoldTemperature(HttpClient client, double currentTemperature, double targetTemperature, int durationSeconds)
    {
        Console.WriteLine($"Holding temperature at {targetTemperature}°C for {durationSeconds} seconds...");
        int intervalMs = 1000; // 1-second intervals

        for (int i = 0; i < durationSeconds; i++)
        {
            if (currentTemperature < targetTemperature)
            {
                // Turn on heaters slightly and reduce fans
                await SetAllHeaters(client, 1); // Minimal heating
                await SetAllFans(client, false); // Reduce cooling
            }
            else if (currentTemperature > targetTemperature)
            {
                // Turn off heaters and increase fans
                await SetAllHeaters(client, 0); // Turn off heating
                await SetAllFans(client, true); // Activate cooling
            }

            // Wait for a second and fetch the updated temperature
            await Task.Delay(intervalMs);
            currentTemperature = await GetAverageTemperature(client);
            Console.WriteLine($"Current Temperature: {currentTemperature:F1}°C");
        }

        return currentTemperature;
    }

    static async Task<double> GetAverageTemperature(HttpClient client)
    {
        // Fetch sensor temperatures and calculate the average
        var sensor1 = double.Parse(await GetSensor1Temperature(client));
        var sensor2 = await GetSensor2Temperature(client);
        var sensor3 = (double)await GetSensor3Temperature(client);

        double avgTemperature = (sensor1 + sensor2 + sensor3) / 3;
        return avgTemperature;
    }

    static async Task SetAllHeaters(HttpClient client, int level)
    {
        for (int i = 1; i <= 3; i++) // Assuming 3 heaters
        {
            await SetHeaterLevel(client, i, level);
        }
    }

    static async Task SetAllFans(HttpClient client, bool state)
    {
        for (int i = 1; i <= 3; i++) // Assuming 3 fans
        {
            await SetFanState(client, i, state);
        }
    }

 

    static async Task<string> GetSensor1Temperature(HttpClient client)
    {
        var response = await client.GetAsync("api/Sensor/sensor1");
        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadAsStringAsync();
        }
        throw new Exception($"Failed to get temperature from Sensor 1: {response.ReasonPhrase}");
    }

    static async Task<int> GetSensor2Temperature(HttpClient client)
    {
        var response = await client.GetAsync("api/Sensor/sensor2");
        if (response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync();
            if (int.TryParse(content, out int temp))
            {
                return temp;
            }
            throw new Exception("Failed to parse Sensor 2 temperature as an integer.");
        }
        throw new Exception($"Failed to get temperature from Sensor 2: {response.ReasonPhrase}");
    }

    static async Task<decimal> GetSensor3Temperature(HttpClient client)
    {
        var response = await client.GetAsync("api/Sensor/sensor3");
        if (response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync();
            if (decimal.TryParse(content, out decimal temp))
            {
                return temp;
            }
            throw new Exception("Failed to parse Sensor 3 temperature as a decimal.");
        }
        throw new Exception($"Failed to get temperature from Sensor 3: {response.ReasonPhrase}");
    }

   

   

    static async Task<double> GetSensorTemperature(HttpClient client, int sensorId)
    {
        var response = await client.GetAsync($"api/sensor/{sensorId}");
        if (response.IsSuccessStatusCode)
        {
            var tempString = await response.Content.ReadAsStringAsync();
            return double.Parse(tempString);
        }

        throw new Exception($"Failed to get temperature from sensor {sensorId}: {response.ReasonPhrase}");
    }

    static async Task SetHeaterLevel(HttpClient client, int heaterId, int level)
    {
        var response = await client.PostAsync($"api/heat/{heaterId}",
            new StringContent(level.ToString(), System.Text.Encoding.UTF8, "application/json"));
        if (!response.IsSuccessStatusCode)
        {
            throw new Exception($"Failed to set heater level {heaterId}: {response.ReasonPhrase}");
        }
    }

    static async Task SetFanState(HttpClient client, int fanId, bool isOn)
    {
        var response = await client.PostAsync($"api/fans/{fanId}",
            new StringContent(isOn.ToString().ToLower(), System.Text.Encoding.UTF8, "application/json"));
        if (!response.IsSuccessStatusCode)
        {
            throw new Exception($"Failed to set fan state for fan {fanId}: {response.ReasonPhrase}");
        }
    }


    public class FanDTO
    {
        public int Id { get; set; }
        public bool IsOn { get; set; }
    }

}
