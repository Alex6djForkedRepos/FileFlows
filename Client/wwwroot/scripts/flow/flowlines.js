class ffFlowLines {
    ioNode = null;
    ioSelected = null;
    ioIsInput = null;
    ioContext = null;
    ioSourceBounds = null;
    ioCanvasBounds = null;
    ioOutputConnections = new Map();
    ioLines = [];
    FlowContainer = null;
    ioCanvas = null;
    ioSelectedConnection = null;
    accentColor = null;
    lineColor = null;
    ioOffset = 14.5;

    reset() {

        if (this.FlowContainer != null && this.FlowContainer.classList.contains('drawing-line') === true)
            this.FlowContainer.classList.remove('drawing-line');
        this.ioNode = null;
        this.ioSelected = null;
        this.ioIsInput = null;
        this.ioContext = null;
        this.ioSourceBounds = null;
        this.ioCanvasBounds = null;
        this.ioLines = [];
        this.ioOutputConnections = new Map();
        this.ioCanvas = null;
        this.ioSelectedConnection = null;
    }

    ioDown(event, isInput) {
        this.ioNode = event.target.parentNode;
        if (!this.FlowContainer)
            this.FlowContainer = document.getElementById('flow-parts');

        if (this.FlowContainer != null && this.FlowContainer.classList.contains('drawing-line') === false)
            this.FlowContainer.classList.add('drawing-line');
        this.ioSelected = this.ioNode.parentNode.parentNode;
        this.ioIsInput = isInput;

        let canvas = document.querySelector('canvas');
        this.ioCanvasBounds = canvas.getBoundingClientRect();
        var srcBounds = this.ioNode.getBoundingClientRect();
        let srcX = (srcBounds.left - this.ioCanvasBounds.left) + this.ioOffset;
        let srcY = (srcBounds.top - this.ioCanvasBounds.top) + this.ioOffset;
        this.ioSourceBounds = { left: srcX, top: srcY };

        if (this.selectedOutput != null) {
        }
        else {
            // start drawing line
        }
    };

    ioMouseMove(event) {
        if (!this.ioNode)
            return;

        let destX = ffFlow.translateCoord(event.clientX, true) - this.ioCanvasBounds.left;
        let destY = ffFlow.translateCoord(event.clientY, true) - this.ioCanvasBounds.top;
        this.redrawLines();
        let overInput = !!document.querySelector('.inputs > div > div:hover');

        this.drawLineToPoint({ srcX: this.ioSourceBounds.left, srcY:this.ioSourceBounds.top, destX, destY, color: overInput ? '#ff0090' : null });
    };


    ioMouseUp(event) {
        if (!this.ioNode)
            return;

        let target = event.target.parentNode;
        let suitable = target?.classList?.contains(this.ioIsInput ? 'output' : 'input') === true;
        if (suitable) {
            let input = this.isInput ? this.ioNode : target;
            let output = this.isInput ? target : this.ioNode;
            let outputId = output.getAttribute('id');

            if (input.classList.contains('connected') === false)
                input.classList.add('connected');
            if (output.classList.contains('connected') === false)
                output.classList.add('connected');

            let connections = this.ioOutputConnections.get(outputId);
            if (!connections) {
                this.ioOutputConnections.set(outputId, []);
                connections = this.ioOutputConnections.get(outputId);
            }
            let index = parseInt(input.getAttribute('x-input'), 10);

            let part = input.parentNode.parentNode.getAttribute('id');
            let existing = connections.filter(x => x.index == index && x.part == part);
            if (!existing || existing.length === 0) {

                if (ffFlow.SingleOutputConnection) {
                    connections = [{ index: index, part: part }];
                    this.ioOutputConnections.set(outputId, connections);
                }
                else
                    connections.push({ index: index, part: part });

                this.drawLine(input, output);
            }
        }

        if (this.FlowContainer != null && this.FlowContainer.classList.contains('drawing-line') === true)
            this.FlowContainer.classList.remove('drawing-line');

        this.ioNode = null;
        this.ioSelected = null;
        this.redrawLines();
    };

    redrawLines() {
        ffFlow.selectConnection();
        this.ioLines = [];
        let outputs = document.querySelectorAll('.flow-part .output');
        for (let o of document.querySelectorAll('.flow-part .output, .flow-part .input'))
            o.classList.remove('connected');
        let canvas = this.getCanvas();
        if (!this.ioContext) {
            this.ioContext = canvas.getContext('2d');
        }
        this.ioContext.clearRect(0, 0, canvas.width, canvas.height);

        for (let output of outputs) {
            let connections = this.ioOutputConnections.get(output.getAttribute('id'));
            if (!connections)
                continue;
            for (let input of connections) {
                let inputEle = document.getElementById(input.part + '-input-' + input.index);
                if (!inputEle)
                    continue;
                this.drawLine(inputEle, output, input);
            }
        }
        this.drawDottedSelection();
    };
    
    drawDottedSelection(context){
        if(window.ffFlow.Mouse.canvasSelecting !== true)
            return;
        let canvas = this.getCanvas();
        let canvasBounds = canvas.getBoundingClientRect();
        
        let x1 = window.ffFlow.Mouse.initialX;
        let y1 = window.ffFlow.Mouse.initialY;
        let x2 = window.ffFlow.Mouse.currentX;
        let y2 = window.ffFlow.Mouse.currentY;
        x1 -= canvasBounds.left;
        y1 -= canvasBounds.top;
        this.ioContext.strokeStyle = this.accentColor;
        this.ioContext.setLineDash([2]);
        this.ioContext.strokeRect(x1, y1, x2, y2);
        this.ioContext.setLineDash([0]);
    }

    drawLine(input, output, connection) {
        if (!input || !output)
            return;

        let src = output;
        let dest = input;
        if (output.classList.contains('connected') == false)
            output.classList.add('connected');
        if (input.classList.contains('connected') == false)
            input.classList.add('connected');
        let srcBounds = src.getBoundingClientRect();
        let destBounds = dest.getBoundingClientRect();

        let canvas = this.getCanvas();
        let canvasBounds = canvas.getBoundingClientRect();
        let srcX = (srcBounds.left - canvasBounds.left) + this.ioOffset;
        let srcY = (srcBounds.top - canvasBounds.top) + this.ioOffset;
        let destX = (destBounds.left - canvasBounds.left) + this.ioOffset;
        let destY = (destBounds.top - canvasBounds.top) + this.ioOffset;
        this.drawLineToPoint({ srcX, srcY, destX, destY, output, connection });
    };

    drawLineToPoint({ srcX, srcY, destX, destY, output, connection, color }) {
        if (!this.ioContext) {
            let canvas = this.getCanvas();
            this.ioContext = canvas.getContext('2d');
        }

        const context = this.ioContext;

        const path = new Path2D();
        if (ffFlow.Vertical) {
            srcX += 2;
            destX += 2;
        }
        path.moveTo(srcX, srcY);

        if (ffFlow.Vertical) {
            if (srcY < destY - 20) {
                // draw stepped line
                let mid = destY + (srcY - destY) / 2;
                path.lineTo(srcX, mid);
                path.lineTo(destX, mid);
                path.lineTo(destX, destY);
            }
            else {
                path.lineTo(srcX, srcY + 20);
                let midX = (destX - srcX) / 2
                path.lineTo(srcX + midX, srcY + 20);
                path.lineTo(srcX + midX, destY - 20);
                path.lineTo(destX, destY - 20);
                path.lineTo(destX, destY);
            }
        } else {
            if (Math.abs(destY - srcY) <= 50) {
                path.lineTo(destX, destY);
            } else {
                path.bezierCurveTo(srcX + 50, srcY + 50,
                    destX - 50, destY - 50,
                    destX, destY);
            }
        }

        context.lineWidth = 5;
        context.strokeStyle = color || this.lineColor;
        context.stroke(path);

        this.ioLines.push({ path: path, output: output, connection: connection });

    };

    getCanvas() {
        if (this.ioCanvas)
            return this.ioCanvas;

        this.accentColor = this.colorFromCssClass('--accent');
        this.lineColor = this.colorFromCssClass('--color-darkest');

        let canvas = document.querySelector('canvas');
        // Listen for mouse moves
        let self = this;
        canvas.addEventListener('mousedown', function (event) {
            let ctx = self.ioContext;
            self.ioSelectedConnection = null;
            let clearNode = true;
            let selectedLine = null;
            for (let line of self.ioLines) {
                // Check whether point is inside ellipse's stroke
                if (!selectedLine && ctx.isPointInStroke(line.path, event.offsetX, event.offsetY)) {
                    selectedLine = line;
                    self.ioSelectedConnection = line;
                    let output = line.output.parentNode.parentNode.getAttribute('id');
                    let outputNode = line.output.getAttribute('x-output');
                    ffFlow.selectConnection(output, outputNode);
                    clearNode = false;
                    event.stopImmediatePropagation();
                    event.stopPropagation();
                    event.preventDefault();
                }
                else {
                    ctx.strokeStyle = self.lineColor;
                    ctx.stroke(line.path);
                }
            }
            if (selectedLine) {
                ctx.strokeStyle = self.accentColor;
                ctx.stroke(selectedLine.path);
            }
            if (clearNode)
                ffFlow.selectConnection();
        });
        canvas.addEventListener('keydown', function (event) {
            if (event.code === 'Delete') 
                self.deleteConnection();            
        });
        this.ioCanvas = canvas;
        return this.ioCanvas;
    }

    deleteConnection() {
        if (!this.ioSelectedConnection)
            return;

        ffFlow.selectConnection();

        let selected = this.ioSelectedConnection;
        let outputNodeUid = selected.output.getAttribute('id');

        if (ffFlow.SingleOutputConnection) {
            this.ioOutputConnections.delete(outputNodeUid);
        } else {
            let connections = this.ioOutputConnections.get(outputNodeUid);
            let index = connections.indexOf(selected.connection);
            if (index >= 0) {
                connections.splice(index, 1);
            }
        }

        this.redrawLines();
    }


    colorFromCssClass(variable) {
        var tmp = document.createElement("div"), color;
        tmp.style.cssText = "position:fixed;left:-100px;top:-100px;width:1px;height:1px";
        if (variable.startsWith('--'))
            tmp.style.color = "var(" + variable + ")";
        else
            tmp.style.color = variable;
        document.body.appendChild(tmp);  // required in some browsers
        color = getComputedStyle(tmp).getPropertyValue("color");
        document.body.removeChild(tmp);
        return color;
    }
}