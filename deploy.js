const { exec } = require("child_process");
const readline = require("readline");

const rl = readline.createInterface ({
    input: process.stdin,
    output: process.stdout
});

function run (command)
{
	return new Promise ((resolve, reject) =>
	{
		console.log('\x1B[32m * ' + command + '\x1B[0m');
		exec(command, (err, stdout) =>
		{
			if (stdout)
				console.log(stdout);

			if (err) {
				console.log('\x1B[31m Error: ' + err + '\x1B[0m');
				reject(err);
				return;
			}

			resolve();
		});
	});
};

rl.question("Version number: ", function(version)
{
	run(r => run('rmdir /s /q temporal'))
	.then(r => run('git clone https://github.com/ironrocksoftware/helicon temporal --no-checkout'))
	.then(r => run('copy dist\* temporal'))
	.then(r => run('cd temporal'))
	.then(r => run('git branch temporal'))
	.then(r => run('git add .'))
	.then(r => run('git commit -m "Preparing for release: v'+version+'"'))
	.then(r => run('git push origin temporal'))
	.then(r => run('git tag -f v' + version))
	.then(r => run('git push --tags'))
	.then(r => run('cd ..'))
	.then(r => run('git push origin --delete temporal'))
	.then(() => {
		console.log();
		console.log('\x1B[93m * Deployment completed: '+version+'\x1B[0m');
		rl.close();
	});
});

rl.on("close", function()
{
	process.exit(0);
});
