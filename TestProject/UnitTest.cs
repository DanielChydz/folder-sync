using folder_sync;

namespace TestProject {
    public class UnitTest {
        public UnitTest() {
            // reset globals before each test
            Globals.updatedSourceFileMap = new Dictionary<string, FileData>();
            Globals.destinationFileMap = new Dictionary<string, FileData>();
            Globals.doFullMd5Check = false;
        }

        [Fact]
        public void ShouldCopyFile_ReturnsTrue_WhenDestinationFileDoesNotExist() {
            // arrange
            var src = "src.txt";
            var dst = "dst.txt";
            Globals.updatedSourceFileMap[src] = new FileData(DateTime.Now, 100);

            // act
            var result = Utils.ShouldCopyFile(src, dst);

            // assert
            Assert.True(result);
        }

        [Fact]
        public void ShouldCopyFile_ReturnsTrue_WhenFileSizesDiffer() {
            // arrange
            var src = "src.txt";
            var dst = "dst.txt";
            Globals.updatedSourceFileMap[src] = new FileData(DateTime.Now, 100);
            Globals.destinationFileMap[dst] = new FileData(DateTime.Now, 200);

            // act
            var result = Utils.ShouldCopyFile(src, dst);

            // assert
            Assert.True(result);
        }

        [Fact]
        public void ShouldCopyFile_ReturnsFalse_WhenFilesAreIdentical() {
            // arrange
            var src = "src.txt";
            var dst = "dst.txt";
            var now = DateTime.Now;
            Globals.updatedSourceFileMap[src] = new FileData(now, 100);
            Globals.destinationFileMap[dst] = new FileData(now, 100);

            // act
            var result = Utils.ShouldCopyFile(src, dst);

            // assert
            Assert.False(result);
        }
    }
}