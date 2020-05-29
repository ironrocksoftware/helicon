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

rl.question("Version number?", function(name)
{
	rl.close();
});

rl.on("close", function()
{
	process.exit(0);
});

run('svn-msg "Published: v'+version+'"')
.then(r => run('git add .'))
.then(r => run('git commit -F .svn\\messages.log'))
.then(r => run('git push'))
.then(r => run('git branch temporal'))
.then(r => run('git checkout temporal'))

.then(r => run('del .gitignore'))
.then(r => run('del deploy.js'))

.then(r => run('del README.md'))



.then(r => run('git commit -a -m "Prepa
ring for release: '+version+'"'))
.then(r => run('git push origin temporal'))
.then(r => run('git tag -f v' + version))
.then(r => run('git push --tags'))
.then(r => run('git checkout master'))
.then(r => run('git branch -D temporal'))
.then(r => run('git push origin --delete temporal'))
//.then(r => run('git reset'))

.then(() => {
	console.log();
	console.log('\x1B[93m * Deployment completed.\x1B[0m');
});
