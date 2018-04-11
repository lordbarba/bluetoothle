﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;


namespace Plugin.BluetoothLE.Tests
{
    public class CharacteristicTests : IDisposable
    {
        readonly ITestOutputHelper output;
        IGattCharacteristic[] characteristics;
        IDevice device;

        public CharacteristicTests(ITestOutputHelper output) => this.output = output;


        async Task Setup()
        {
            this.device = await CrossBleAdapter
                .Current
                .ScanUntilDeviceFound(Constants.DeviceName)
                .Timeout(TimeSpan.FromSeconds(5000))
                .ToTask();

            await this.device.ConnectWait().ToTask();

            this.characteristics = await this.device
                .GetCharacteristicsForService(Constants.ScratchServiceUuid).Take(5)
                .ToArray()
                .ToTask();
        }


        public void Dispose()
        {
            this.device?.CancelConnection();
        }


        [Fact]
        public async Task WriteWithoutResponse()
        {
            await this.Setup();

            var value = new byte[] { 0x01, 0x02 };
            foreach (var ch in this.characteristics)
            {
                var write = await ch.WriteWithoutResponse(value);
                Assert.True(write.Success, "Write failed - " + write.ErrorMessage);

                // TODO: enable write back on host
                //var read = await ch.Read();
                //read.Success.Should().BeTrue("Read failed - " + read.ErrorMessage);

                //read.Data.Should().BeEquivalentTo(value);
            }
        }


        [Fact]
        public async Task Concurrent_Notifications()
        {
            await this.Setup();
            var list = new Dictionary<Guid, int>();

            this.characteristics
                .ToObservable()
                .Select(x => x.RegisterAndNotify(true))
                .Merge()
                .Synchronize()
                .Subscribe(x =>
                {
                    var id = x.Characteristic.Uuid;
                    if (list.ContainsKey(id))
                    {
                        list[id]++;
                        this.output.WriteLine("Existing characteristic reply - " + id);
                    }
                    else
                    {
                        list.Add(id, 1);
                        this.output.WriteLine("New characteristic reply - " + id);
                    }
                });

            await Task.Delay(10000);

            Assert.True(list.Count >= 2, "There were not at least 2 characteristics in the replies");
            Assert.True(list.First().Value >= 2, "First characteristic did not speak at least 2 times");
            Assert.True(list.ElementAt(2).Value >= 2, "Second characteristic did not speak at least 2 times");
        }


        [Fact]
        public async Task Concurrent_Writes()
        {
            await this.Setup();
            var bytes = new byte[] { 0x01 };

            var t1 = this.characteristics[0].Write(bytes).ToTask();
            var t2 = this.characteristics[1].Write(bytes).ToTask();
            var t3 = this.characteristics[2].Write(bytes).ToTask();
            var t4 = this.characteristics[3].Write(bytes).ToTask();
            var t5 = this.characteristics[4].Write(bytes).ToTask();

            await Task.WhenAll(t1, t2, t3, t4, t5);

            Assert.True(t1.Result.Success, "1 failed");
            Assert.True(t2.Result.Success, "2 failed");
            Assert.True(t3.Result.Success, "3 failed");
            Assert.True(t4.Result.Success, "4 failed");
            Assert.True(t5.Result.Success, "5 failed");
        }


        [Fact]
        public async Task Concurrent_Reads()
        {
            await this.Setup();
            var t1 = this.characteristics[0].Read().ToTask();
            var t2 = this.characteristics[1].Read().ToTask();
            var t3 = this.characteristics[2].Read().ToTask();
            var t4 = this.characteristics[3].Read().ToTask();
            var t5 = this.characteristics[4].Read().ToTask();

            await Task.WhenAll(t1, t2, t3, t4, t5);

            Assert.True(t1.Result.Success, "1 failed");
            Assert.True(t2.Result.Success, "2 failed");
            Assert.True(t3.Result.Success, "3 failed");
            Assert.True(t4.Result.Success, "4 failed");
            Assert.True(t5.Result.Success, "5 failed");
        }


        [Fact]
        public async Task NotificationFollowedByWrite()
        {
            await this.Setup();

            var write = await this.characteristics.First()
                .RegisterAndNotify()
                .Select(x => x.Characteristic.Write(new byte[] {0x0}))
                .Switch()
                .FirstOrDefaultAsync();

            Assert.True(write.Success);
        }
    }
}
