import { FFChart } from './FFChart.js';

/**
 * Represents the Processing class.
 * @extends FFChart
 */
export class Processing extends FFChart
{
    recentlyFinished;
    timer;
    existing;
    runners = {};
    isPaused;
    eventListener;

    /**
     * Constructs a new Processing instance.
     * @param {string} uid - The UID of the Processing instance.
     * @param {Object} args - The arguments for the Processing instance.
     */
    constructor(uid, args) {        
        super(uid, args);
        this.recentlyFinished = args.flags === 1;
        this.createNoData();
        this.eventListener = (event) => this.onExecutorsUpdated(event);
        document.addEventListener("onClientServiceUpdateExecutors", this.eventListener);
    }
    
    /**
     * Disposes of the Processing instance.
     */
    dispose() {
        super.dispose();
        document.removeEventListener("onClientServiceUpdateExecutors", this.eventListener);
    }

    /**
     * Fetches data asynchronously.
     */
    async fetchData(){
    }

    /**
     * Called when the websocket receives an update to the executors.
     * @param {Event} event - The event from the websocket.
     */
    onExecutorsUpdated(event) {
        let data = event?.detail?.data;
        if(!data)
            return;
        this.createChart(data);
    }

    /**
     * Gets data asynchronously.
     */
    async getData() {
    }

    /**
     * Creates a chart for the runner.
     * @param {Object[]} data - The data for the chart.
     */
    createChart(data) {
        // check if the data has changed
        let json = (data ? JSON.stringify(data) : '') + (':' + this.isPaused);
        if(json === this.existing)
            return;
        this.existing = json; // so we dont refresh if we don't have to
        let title = 'FileFlows - Dashboard';
        if(data?.length)
        {
            if(this.hasNoData)
            {
                let chartDiv = document.getElementById(this.chartUid);
                if(chartDiv)
                    chartDiv.textContent = '';
                this.hasNoData = false;
            }
            this.createRunners(data);
            let first = data[0];
            if(first.currentPartPercent > 0)
                title = 'FileFlows - ' + first.currentPartPercent.toFixed(1) + ' %';
            else if(first.currentPartName)
                title = 'FileFlows - ' + first.currentPartName;
            else
                title = 'FileFlows - Dashboard';
        }
        else if(!this.hasNoData)
            this.createNoData();

        document.title = title;

        this.setSize(data?.length);
    }

    /**
     * Sets the size of the widget.
     * @param {number} size - The size in terms of how many rows.
     */
    setSize(size) {
        let rows = Math.floor((size - 1) / 2) + 1;
        ffGrid.update(this.ele, { h: rows});
    }


    /**
     * Creates the no data element when no runners are running.
     */
    createNoData(){
        this.hasNoData = true;
        let chartDiv = document.getElementById(this.chartUid);
        chartDiv.textContent = '';

        let div = document.createElement('div');
        div.className = 'no-data';

        let span = document.createElement('span');
        div.appendChild(span);

        let icon = document.createElement('i');
        span.appendChild(icon);

        let spanText = document.createElement('span');
        span.appendChild(spanText);
        if(this.isPaused){
            icon.className = 'fas fa-pause';
            spanText.innerText = 'Processing is currently paused';
        }else {
            icon.className = 'fas fa-times';
            spanText.innerText = 'No files currently processing';
        }

        chartDiv.appendChild(div);
    }


    /**
     * Creates the runners for the data.
     * @param {Object[]} data - The data from the websocket.
     */
    createRunners(data) {
        let running = [];
        let chartDiv = document.getElementById(this.chartUid);
        if(!chartDiv)
            return;
        chartDiv.className = 'processing-runners runners-' + data.length;
        for(let worker of data)
        {
            running.push(worker.uid);
            if(!this.runners[worker.uid]) {
                // new, create it
                this.runners[worker.uid] = new Runner(chartDiv, this.csharp, worker);
            }
            this.runners[worker.uid].update({data: worker, totalRunners: data.length});
        }
        let keys = Object.keys(this.runners);
        for(let i=keys.length; i >= 0; i--){
            let key = keys[i];
            if(!key)
                continue;
            if(running.indexOf(key) < 0){
                this.runners[key].dispose();
                delete this.runners[key];
            }
        }
    }
}


/**
 * A runner in the processing tab
 */
class Runner {
    /**
     * Constructs a new runner
     * @param {HTMLElement} parent - The parent element to attach this new runner to
     * @param {Object} csharp - The csharp instance to call csharp methods
     * @param {Object} runner - The runner being executed
     */
    constructor(parent, csharp, runner) {
        this.uid = runner.uid;
        this.eleChartId = `runner-${this.uid}-chart`;
        this.libraryFile = runner.libraryFile;
        this.library = runner.library;
        this.csharp = csharp;
        this.createElement(parent);

        this.eleChart = document.getElementById(this.eleChartId);
        this.infoElements = {
            file: this.element.querySelector(".info-file"),
            node: this.element.querySelector(".info-node"),
            library: this.element.querySelector(".info-library"),
            step: this.element.querySelector(".info-step"),
            time: this.element.querySelector(".info-time"),
        };
    }

    /**
     * Event handler for log button click
     */
    logClicked = () => {
        this.csharp.invokeMethodAsync("OpenFileViewer", this.libraryFile.uid);
    };

    /**
     * Event handler for cancel button click
     */
    cancelClicked = async () => {
        this.csharp.invokeMethodAsync(
            "CancelRunner",
            this.uid,
            this.libraryFile.uid,
            this.libraryFile.name
        );
    };


    /**
     * Updates the runner with new data
     * @param {Object} data - The runner data
     * @param {number} totalRunners - The total runners
     */
    update = ({ data, totalRunners }) => {
        this.updateInfo(data);
        this.createOrUpdateRadialBar({
            totalParts: data.totalParts,
            currentPart: data.currentPart,
            currentPartPercent: data.currentPartPercent,
            totalRunners: totalRunners,
        });
    };

    /**
     * Updates the runner info
     * @param {Object} runner - The runner
     */
    updateInfo = (runner) => {
        const step = this.humanizeStepName(runner.currentPartName);
        const time = this.timeDiff(Date.parse(runner.startedAt), Date.now());

        this.infoElements.file.textContent = runner.libraryFile?.name || "";
        this.infoElements.node.textContent = runner.nodeName || "";
        this.infoElements.library.textContent = runner.library?.name || "";
        this.infoElements.step.textContent = step || "";
        this.infoElements.time.textContent = time || "";
    };

    /**
     * Creates the HTML elements for the runner
     * @param {HTMLElement} parent - The parent to attach the runner to
     */
    createElement(parent) {
        this.element = document.createElement("div");
        this.element.className = "runner";
        this.element.id = `runner-${this.uid}`;
        parent.appendChild(this.element);

        this.element.innerHTML = `
      <div class="chart chart-${this.uid}" id="${this.eleChartId}"></div>
      <div class="info">
        <div class="lv w-2 file">
          <span class="l">File</span>
          <span class="v info-file"></span>
        </div>
        <div class="lv node">
          <span class="l">Node</span>
          <span class="v info-node"></span>
        </div>
        <div class="lv library">
          <span class="l">Library</span>
          <span class="v info-library"></span>
        </div>
        <div class="lv step">
          <span class="l">Step</span>
          <span class="v info-step"></span>
        </div>
        <div class="lv time">
          <span class="l">Time</span>
          <span class="v info-time"></span>
        </div>
      </div>
      <div class="buttons">
        <button class="btn btn-log" onclick="logClicked">Info</button>
        <button class="btn btn-cancel" onclick="cancelClicked">Cancel</button>
      </div>
    `;


        this.element.querySelector(".btn-log").addEventListener(
            "click",
            this.logClicked
        );
        this.element.querySelector(".btn-cancel").addEventListener(
            "click",
            this.cancelClicked
        );
    }

    /**
     * Creates or updates the radial bar chart for the runner
     * @param {Object} options - Chart options
     */
    createOrUpdateRadialBar(options) {
        const { totalParts, currentPart, currentPartPercent, totalRunners } = options;

        const overall = totalParts === 0 ? 100 : (currentPart / totalParts) * 100;
        const chartOptions = {
            chart: {
                id: this.eleChartId,
                height: totalRunners > 3 ? "200px" : "190px",
                type: "radialBar",
                foreColor: "var(--color)",
            },
            plotOptions: {
                radialBar: {
                    hollow: {
                        margin: 5,
                        size: "48%",
                        background: "transparent",
                    },
                    track: {
                        background: "#333",
                    },
                    startAngle: -135,
                    endAngle: 135,
                    stroke: {
                        lineCap: "round",
                    },
                    dataLabels: {
                        total: {
                            show: true,
                            label: currentPartPercent ? `${currentPartPercent.toFixed(1)} %` : "Overall",
                            fontSize: "0.8rem",
                            formatter: (val) => parseFloat("" + overall).toFixed(1) + " %",
                        },
                        value: {
                            show: true,
                            fontSize: "0.7rem",
                            formatter: (val) => +(parseFloat(val).toFixed(1)) + " %",
                        },
                    },
                },
            },
            colors: ["#2b8fb3", "#c30471"],
            series: [overall],
            labels: ["Overall"],
        };

        if (currentPartPercent > 0) {
            chartOptions.series.push(currentPartPercent);
            chartOptions.labels.push("Current");
        }

        let updated = false;

        if (this.eleChart.querySelector(".apexcharts-canvas")) {
            try {
                ApexCharts.exec(this.eleChartId, "updateOptions", chartOptions, false, false);
                updated = true;
            } catch (err) {}
        }

        if (!updated && this.eleChart) {
            new ApexCharts(this.eleChart, chartOptions).render();
        }
    }

    /**
     * Gets the time difference between two dates
     * @param {number} start - The start date
     * @param {number} end - The end date
     * @returns {string} The time difference as a string
     */
    timeDiff(start, end) {
        let diff = (end - start) / 1000;
        let hours = Math.floor(diff / 3600);
        diff -= hours * 3600;
        let minutes = Math.floor(diff / 60);
        diff -= minutes * 60;
        let seconds = Math.floor(diff);

        return (
            hours.toString().padStart(2, "0") +
            ":" +
            minutes.toString().padStart(2, "0") +
            ":" +
            seconds.toString().padStart(2, "0")
        );
    }

    /**
     * Humanizes the step name
     * @param {string} step - The name fo the step
     * @returns {string} The humanized name
     */
    humanizeStepName(step) {
        if (!step || step.trim() === "") {
            return "Starting...";
        }

        return step
            .trim()
            .replace(/([A-Z]+|[0-9]+)/g, " $1") // Add spaces before uppercase letters and numbers
            .replace(/^\s+|\s+$/g, "") // Trim leading and trailing spaces
            .replace(/\b\w/g, (match) => match.toUpperCase()) // Capitalize the first letter of each word
            .replace("Ffmpeg", "FFMPEG"); // Replace specific word
    }

    /**
     * Disposes of the runner by removing its HTML element
     */
    dispose() {
        this.element.remove();
    }
}
